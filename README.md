# SRSBrain

A Varian Eclipse ESAPI plugin that automates treatment geometry setup for **single-isocentre, multi-target brain SRS (stereotactic radiosurgery) VMAT planning**.

## Why

Setting up a multi-metastasis brain SRS plan involves several repetitive, error-prone manual steps:

- **Isocentre placement.** For a single-isocentre technique, the isocentre should be positioned so that all targets stay close to the central axis (geometric accuracy degrades off-axis). Eyeballing this is slow and suboptimal, and it isn't always obvious whether one isocentre is even feasible for a given set of lesions.
- **Field arrangement.** Departments typically use standard non-coplanar arc templates (couch kicks), which are tedious to recreate by hand for every plan.
- **Collimator angle selection.** With multiple targets sharing one isocentre, a poor collimator angle forces single MLC leaf pairs to span two targets at once, opening up "islands" of normal tissue between targets that the MLC cannot shield. Choosing good collimator angles per arc materially reduces dose to healthy brain.
- **Patient-specific QA feasibility.** Widely separated targets may not physically fit on a MapCheck detector array during QA — better to find out at planning time than at the QA session.

SRSBrain wraps all of this into one tool that runs inside Eclipse against the currently open plan.

## Functionality

The plugin opens a window ("SRS Brain Treatment Geometry") listing all PTV/CTV/GTV structures in the current plan's structure set (likely composite/ring/boolean targets are sorted to the bottom). The user ticks the targets to treat and can then:

### 1. Check isocentre suitability
For the selected targets, the tool evaluates how many isocentres are needed:
- It computes the **minimal enclosing sphere** (miniball) of all target mesh points. If the radius exceeds 70 mm, the targets are flagged as too far off-axis for a single isocentre.
- It checks **MapCheck QA feasibility** for each target pair (a target ~10 cm superior of another, within the detector's lateral footprint, can collide with the detector electronics).
- If one isocentre has issues, it clusters target centroids with **k-means** (Accord.NET, best of 10 restarts) into 2, 3, … groups and repeats the checks per group, reporting a suggested grouping of PTVs per isocentre.

### 2. Run the script (geometry automation)
- **Field arrangement** — choose one of:
  - *Use existing geometry* (keep the plan's current beams),
  - *45° couch kicks* — up to five half arcs at couch 0/45/90/315 (selectable via checkboxes on a head diagram),
  - *60° couch kicks* — up to four half arcs at couch 0/60/300.
- **Automated isocentre placement** (optional) — sets the isocentre to the centre of the targets' minimal enclosing sphere (refuses and warns if the bounding radius is > 70 mm); otherwise reuses the isocentre of the existing plan's beams.
- **Collimator optimisation** (optional) — for each unique arc geometry, simulates the beam's-eye view of all target meshes across every control point and every collimator angle (5°–175°), bins projected points into the 60 MLC leaf-pair rows (Millennium 120 layout: 10 mm outer, 5 mm central leaves), and scores each angle by total "island" area (unshieldable gaps between targets within a leaf pair) with total open area as a tie-breaker. Paired arcs sharing a geometry get the best two angles at least 10° apart. The calculation runs in parallel with a progress bar.
- The plan's dose calculation model (Acuros AXB, 1.25 mm grid, GPU), optimiser model and SRS-appropriate options are set automatically, and a warning dialog is raised at run time if any selected target pair looks un-QA-able on MapCheck.

## Repository structure

```
SRSBrain.sln
├── Plugin/                  # The ESAPI plugin (builds SRSBrain.esapi.dll), MVVM layout
│   ├── Plugin.cs            #   Script entry point (VMS.TPS.Script via EsapiEssentials)
│   ├── Models/
│   │   ├── ContextModel.cs        # Plan/beam manipulation, iso checks, MapCheck checks
│   │   ├── BoundingSphere.cs      # Minimal enclosing sphere of target meshes (miniball)
│   │   ├── KMeans.cs              # Target clustering into isocentre groups (Accord.NET)
│   │   ├── OptimalCollimator.cs   # BEV projection + MLC island-area collimator optimiser
│   │   └── FieldArrangement.cs    # SimpleBeam descriptor (arc length, gantry, couch)
│   ├── ViewModels/SRSViewModel.cs # UI state, commands, beam template definitions
│   ├── Views/SRSView.xaml         # Target list, arrangement picker, run button
│   └── Resources/                 # Head diagrams and icon
├── PluginRunner_WPF/        # Standalone WPF harness for running/debugging the plugin
│                            # outside Eclipse (EsapiEssentials.PluginRunner)
└── miniball_csharp/         # C# port of the "miniball" smallest-enclosing-ball
    ├── miniball/            # algorithm (SEB namespace), with example and tests
    ├── example/
    └── test/
```

## Building

- Visual Studio solution, **.NET Framework 4.8**, the plugin must be built **x64** (to match Eclipse).
- References the Varian **ESAPI** assemblies (`VMS.TPS.Common.Model.API/Types`) via a relative hint path (`..\..\..\esapi\API\`) — adjust to your ESAPI installation.
- NuGet packages: EsapiEssentials, MvvmLightLibs, Accord / Accord.MachineLearning, MathNet.Numerics, ParallelExtensionsExtras (restore via NuGet).
- Output is `SRSBrain.esapi.dll`, a writeable ESAPI plugin (`[assembly: ESAPIScript(IsWriteable = true)]`); run it from Eclipse's script window, or debug via the `PluginRunner_WPF` harness.

## Important caveats

- **Clinic-specific values are hard-coded** in `ContextModel.cs`: treatment machine `"Acacia"`, 6X FFF at 1400 MU/min, `SRS ARC` technique, dose model `AXB_16.1_1,0.5`, optimiser `PO_1610` (falling back to `PO_16.1`). Adapt these before use elsewhere. The MapCheck feasibility thresholds (100 mm longitudinal, ±23 mm / ±51 mm lateral) encode a specific detector geometry.
- **This is not a validated medical device.** It modifies treatment plans (removes/adds beams, moves isocentres, changes calculation models). Any clinical use requires thorough commissioning and independent checks per your department's processes.
