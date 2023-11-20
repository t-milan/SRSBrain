using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EsapiEssentials.Plugin; //PluginScriptContext context
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows;
using System.Numerics;


namespace Plugin.Models
{
    public enum IsoPlacement
    {
        BoundingSphere,
        CentreOfMass,
        PreviousPlan,
    }

    public class ContextModel
    {
        public PluginScriptContext Context;
        public List<Structure> SelectedTargets;
        private ExternalBeamMachineParameters ebmp;
        public ExternalPlanSetup newPlan;

        private double[] halfArcMetersets = new double[91];
        private double[] fullArcMetersets = new double[181];
        


        public ContextModel(PluginScriptContext context)
        {
            Context = context;
            SelectedTargets = new List<Structure>();

            ebmp = new ExternalBeamMachineParameters("Acacia", "6X", 1400, "SRS ARC", "FFF");

            for (int i = 0; i < 91; i++)
                halfArcMetersets[i] = i / 90.0;
            for (int i = 0; i < 181; i++)
                fullArcMetersets[i] = i / 180.0;
        }

        public void CreatePlanNew()
        {
            Context.Patient.BeginModifications();
            newPlan = Context.ExternalPlanSetup;

            newPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, "AXB_16.1_1,0.5");
            newPlan.SetCalculationOption("AXB_16.1_1,0.5", "CalculationGridSizeInCM", "0.125");
            newPlan.SetCalculationOption("AXB_16.1_1,0.5", "CalculationGridSizeInCMForSRSAndHyperArc", "0.125");
            newPlan.SetCalculationOption("AXB_16.1_1,0.5", "UseGPU", "Yes");

            try  {
                newPlan.SetCalculationModel(CalculationType.PhotonOptimization, "PO_1610"); // Clinical 
            } catch {
                newPlan.SetCalculationModel(CalculationType.PhotonOptimization, "PO_16.1"); // Tbox
            }

                newPlan.SetCalculationOption("PO_1610", "General/OptimizerSettings/DoseCalculationResolution", "High");
            newPlan.SetCalculationOption("PO_1610", "General/OptimizerSettings/DoseCalculationResolutionForSRSAndHyperarc", "High");
            newPlan.SetCalculationOption("PO_1610", "General/OptimizerSettings/UseGPU", "Yes");
            newPlan.SetCalculationOption("PO_1610", "VMAT/ApertureShapeController", "Moderate");
        }

        public VVector GetIsocenter(IsoPlacement isoPlacement)
        {
            if (isoPlacement == IsoPlacement.BoundingSphere)
            {
                var sphere = BoundingSphere.SphereFromTargets(SelectedTargets);
                //VVector origin = Context.ExternalPlanSetup.StructureSet.Image.UserOrigin;
                //double user_x = sphere.Center.X - origin.x;
                //double user_y = sphere.Center.Y - origin.y;
                //double user_z = sphere.Center.Z - origin.z;
                
                return new VVector(sphere.X, sphere.Y, sphere.Z);
            }
            else if (isoPlacement == IsoPlacement.CentreOfMass)
            {
                return CentreOfMass(SelectedTargets);
            }
            else // Previous Plan
            {
                return Context.ExternalPlanSetup.Beams.First(b => !b.IsSetupField).IsocenterPosition;
                // TODO: Possible error handling if there's no beams?
            }
            
        }

        public string fixId(string desiredId)
        {
            int suffix = 1;
            // Check if the plan ID already exists, and find a unique plan ID
            while (newPlan.Beams.Any(b => b.Id == desiredId))    
            {
                desiredId = desiredId + "_" + suffix;
                suffix++;
            }
            return desiredId;
        }

        public void AddBeams(IsoPlacement isoPlacement, List<SimpleBeam> beams)
        {
            VVector isocenter = GetIsocenter(isoPlacement);// new VVector(x, y, z);

            List<Beam> oldBeams = newPlan.Beams.ToList();

            if (beams.Count == 0) // Use existing beams
            {
                foreach (var pb in oldBeams)
                {
                    var bp = pb.GetEditableParameters();
                    bp.Isocenter = isocenter;
                    pb.ApplyParameters(bp);
                }

                return;
            }
            // Otherwise, remove beams and add new ones. 

            foreach(var pb in oldBeams)
            {
                newPlan.RemoveBeam(pb);
            }
               
            foreach(var b in beams)
            {
                if (b.arcLen == ArcLen.Full)
                {
                    var nb = newPlan.AddVMATBeam(ebmp, fullArcMetersets, 0, b.gStart, b.gStop, b.gDir, b.couch, isocenter);
                    nb.Id = fixId(b.Id); // not necessary if delete fields
                }
                else
                {
                    var nb = newPlan.AddVMATBeam(ebmp, halfArcMetersets, 0, b.gStart, b.gStop, b.gDir, b.couch, isocenter);
                    nb.Id = fixId(b.Id);
                }
            }
        }

        public List<double> GetMetersets(Beam beam)
        {
            int n = beam.ControlPoints.Count; 
            List<double> metersets = new List<double>();
            foreach (var cp in beam.ControlPoints)
            {
                metersets.Add(cp.MetersetWeight);
            }
            return metersets;
        }

        public void ChangeBeamCol(Beam oldBeam, int col)
        {
            var msets = GetMetersets(oldBeam);
            string id = oldBeam.Id;
            Beam newBeam = newPlan.AddVMATBeam(ebmp, 
                msets, 
                col, 
                oldBeam.ControlPoints.First().GantryAngle, 
                oldBeam.ControlPoints.Last().GantryAngle, 
                oldBeam.GantryDirection, 
                oldBeam.ControlPoints.First().PatientSupportAngle, 
                oldBeam.IsocenterPosition);
            newPlan.RemoveBeam(oldBeam);
            newBeam.Id = id;
        }


        public async Task SetOptimalCollimator(EventHandler<int> progressChanged)
        {
            // CREATE DICT (COUCH, GSTART, GSTOP, GDIR) → LIST<BEAM>
            var geoms = new Dictionary<(double, double, double), List<Beam>>();

            // POPULATE DICT
            foreach(var b in newPlan.Beams)
            {
                // ignore non-MLC and setup fields
                if (b.IsSetupField) continue;
                if (b.MLC == null) continue;

                var lower = Math.Min(b.ControlPoints.First().GantryAngle, b.ControlPoints.Last().GantryAngle);
                var upper = Math.Max(b.ControlPoints.First().GantryAngle, b.ControlPoints.Last().GantryAngle);
                var key = (b.ControlPoints[0].PatientSupportAngle, Math.Round(lower,1), Math.Round(upper, 1));
                //var key = (b.ControlPoints[0].PatientSupportAngle, b.ControlPoints.First().GantryAngle, b.ControlPoints.Last().GantryAngle);
                if (geoms.ContainsKey(key))
                {
                    geoms[key].Add(b);
                }
                else 
                {
                    var l = new List<Beam>();
                    l.Add(b);
                    geoms.Add(key, l);
                }
            }

            int numGs = geoms.Count;
            int n = 0;
            foreach(var geom in geoms)
            {
                var optimalCol = new OptimalCollimator(geom.Value[0]);
                optimalCol.ProgressChanged += progressChanged;
                await optimalCol.Calculate(SelectedTargets, n, numGs); // expensive function
                // change beam colly angles... (1 or several...)
                if (geom.Value.Count == 2)
                {
                    var x = optimalCol.Result.OptimalCols();
                    ChangeBeamCol(geom.Value[0], x.Item1);
                    ChangeBeamCol(geom.Value[1], x.Item2);
                } else if (geom.Value.Count == 1)
                {
                    ChangeBeamCol(geom.Value[0], optimalCol.Result.OptimalCol());
                }
                else
                {
                    // PANIC PANIC PANIC TODO
                }
                
                n += 1;
            }
        
        }

        public VVector CentreOfMass(List<Structure> selectedTargets)
        {
            List<Vector3> pts = new List<Vector3>();
            foreach (var s in selectedTargets)
            {
                var mesh = s.MeshGeometry;
                foreach (var p in mesh.Positions)
                {
                    pts.Add(new Vector3((float)p.X, (float)p.Y, (float)p.Z));
                }
            }

            if (pts.Count == 0)
            {
                throw new ArgumentException("No points found in the provided structures.");
            }

            Vector3 sum = Vector3.Zero;
            foreach (var pt in pts)
            {
                sum += pt;
            }

            Vector3 centreOfMass = sum / pts.Count;
            return new VVector(centreOfMass.X, centreOfMass.Y, centreOfMass.Z);
        }
    }
}
