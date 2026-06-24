using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using System.Windows;
using System.Numerics;
using System.Windows.Interop;
using System.Runtime.ConstrainedExecution;

namespace Plugin.Models
{
    public enum IsoPlacement
    {
        BoundingSphere,
        CentreOfMass,
        PreviousPlan,
    }

    public class MapCheckError
    {
        public string QAPTV;
        public string ConflictPTV;
    }

    public class ContextModel
    {
        public ScriptContext Context;
        public List<Structure> SelectedTargets;
        private ExternalBeamMachineParameters ebmp;
        public ExternalPlanSetup newPlan;

        private double[] halfArcMetersets = new double[91];
        private double[] fullArcMetersets = new double[181];
        


        public ContextModel(ScriptContext context)
        {
            Context = context;
            SelectedTargets = new List<Structure>();

            ebmp = new ExternalBeamMachineParameters("MARRI", "6X", 1400, "SRS ARC", "FFF");

            for (int i = 0; i < 91; i++)
                halfArcMetersets[i] = i / 90.0;
            for (int i = 0; i < 181; i++)
                fullArcMetersets[i] = i / 180.0;
        }

        public void CreatePlanNew()
        {
            Context.Patient.BeginModifications();
            newPlan = Context.ExternalPlanSetup;

            newPlan.SetCalculationModel(CalculationType.PhotonVolumeDose, "AXB_18.0_0.5,0.7");
            newPlan.SetCalculationOption("AXB_18.0_0.5,0.7", "CalculationGridSizeInCM", "0.1");
            newPlan.SetCalculationOption("AXB_18.0_0.5,0.7", "CalculationGridSizeInCMForSRSAndHyperArc", "0.1");
            newPlan.SetCalculationOption("AXB_18.0_0.5,0.7", "UseGPU", "Yes");

            try  {
                newPlan.SetCalculationModel(CalculationType.PhotonOptimization, "PO_18.0_0.5,0.7"); // Clinical 

                newPlan.SetCalculationOption("PO_18.0_0.5,0.7", "General/OptimizerSettings/DoseCalculationResolution", "High");
                newPlan.SetCalculationOption("PO_18.0_0.5,0.7", "General/OptimizerSettings/DoseCalculationResolutionForSRSAndHyperarc", "High");
                newPlan.SetCalculationOption("PO_18.0_0.5,0.7", "General/OptimizerSettings/UseGPU", "Yes");
                newPlan.SetCalculationOption("PO_18.0_0.5,0.7", "VMAT/ApertureShapeController", "Moderate");
            } catch {
                newPlan.SetCalculationModel(CalculationType.PhotonOptimization, "PO_16.1"); // Tbox

                newPlan.SetCalculationOption("PO_16.1", "General/OptimizerSettings/DoseCalculationResolution", "High");
                newPlan.SetCalculationOption("PO_16.1", "General/OptimizerSettings/DoseCalculationResolutionForSRSAndHyperarc", "High");
                newPlan.SetCalculationOption("PO_16.1", "General/OptimizerSettings/UseGPU", "Yes");
                newPlan.SetCalculationOption("PO_16.1", "VMAT/ApertureShapeController", "Moderate");
            }

            
        }


        public VVector CalcOptimalIso(IsoPlacement isoPlacement)
        {
            if (isoPlacement == IsoPlacement.BoundingSphere)
            {
                var sphere = BoundingSphere.SphereFromTargets(SelectedTargets);
                if (false) // DEBUG
                    MessageBox.Show("Sphere radius: " + sphere.Radius.ToString());
                if (sphere.Radius > 70)
                {
                    MessageBox.Show("PTVs too far off-axis. Multiple isocentres required.", "Warning", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return new VVector(double.NaN, double.NaN, double.NaN);
                }
                
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
                // TODO: Also return false if the radius ends up being >70?
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

        public bool AddBeams(IsoPlacement isoPlacement, List<SimpleBeam> beams)
        {
            VVector isocenter = CalcOptimalIso(isoPlacement);// new VVector(x, y, z);
            // If bounding radius was >70, don't do anything & return false
            if (double.IsNaN(isocenter.x))
            {
                return false;
            }

            // Do something with isocenter & PTVs!!!!!!!!!!!!!!!
            WarnPhysics(isocenter);

            List<Beam> oldBeams = newPlan.Beams.ToList();

            if (beams.Count == 0) // Use existing beams
            {
                foreach (var pb in oldBeams)
                {
                    var bp = pb.GetEditableParameters();
                    bp.Isocenter = isocenter;
                    pb.ApplyParameters(bp);
                }

                return true;
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

            return true;
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

        private string CheckSingleIsoPTVs(List<Structure> PTVgroup)
        {
            if (PTVgroup.Count == 0)
            {
                MessageBox.Show("Error: Shouldn't get here!");
            }
            string msg = "";
            var sphere = BoundingSphere.SphereFromTargets(PTVgroup);
            if (sphere.Radius > 70)
                msg += "Warning: >7cm off-axis - not suitable.";
            var mapCheckErrors = GetMapcheckErrors(PTVgroup);
            foreach (var e in mapCheckErrors)
            {
                msg += $" {e.QAPTV} may not fit on MapCheck due to position of {e.ConflictPTV}.";
            }
            //return "Warning: >7cm radius. Warning: possily can't be QA'd on MapCheck.";
            return msg;
        }

        private List<Structure> GetTargetSubset(List<Structure> selectedTargets, List<string> PTVIDs) 
        { 
            var newList = new List<Structure>();
            foreach (var s in selectedTargets)
            {
                if (PTVIDs.Contains(s.Id))
                    newList.Add(s);
            }
            return newList;
        }

        
        public void CheckIsocentrePTVs()
        {
            string checkIsoMessage = "";
            string errorCheck;
            bool lookAtNextIso = true;
            for (int i = 1; i <= SelectedTargets.Count; i++)
            {
                if (!lookAtNextIso)
                    break;
                lookAtNextIso = false;
                
                checkIsoMessage += $"{i} iso(s)\n";
                if (i == 1)
                {
                    checkIsoMessage += "        ";
                    errorCheck = CheckSingleIsoPTVs(SelectedTargets);
                    if (String.IsNullOrEmpty(errorCheck))
                    {
                        checkIsoMessage += "No issues detected";
                        break;
                    }
                    else
                    {
                        checkIsoMessage += errorCheck;
                        lookAtNextIso = true;
                    }
                        
                }
                else
                {
                    var k = new KMeans(SelectedTargets, i);
                    int isoNum = 1;
                    
                    // k.WriteMessage();

                    foreach (var pair in k.clusterDictionary)
                    {
                        checkIsoMessage += $"    Group {isoNum}";
                        checkIsoMessage += " (" + string.Join(", ", pair.Value) + ")";
                        checkIsoMessage += "\n        ";
                        var PTVsubset = GetTargetSubset(SelectedTargets, pair.Value);
                        //MessageBox.Show("Looking at " + string.Join(", ", pair.Value)); // DEBUG
                        errorCheck = CheckSingleIsoPTVs(PTVsubset);
                        if (String.IsNullOrEmpty(errorCheck))
                            checkIsoMessage += "No issues detected.";
                        else
                        {
                            checkIsoMessage += errorCheck;
                            lookAtNextIso = true;
                        }
                        checkIsoMessage += "\n";
                        isoNum += 1;
                    }
                }

                checkIsoMessage += "\n\n";

            }
            MessageBox.Show(checkIsoMessage);
        }

        class TargetPositionData
        {
            public VVector cx;
            public double Xmin; public double Xmax;
            public double Ymin; public double Ymax;
            public double Zmin; public double Zmax;
        }

        
        public List<MapCheckError> GetMapcheckErrors(List<Structure> PTVsubset)
        {
            var result = new List<MapCheckError>();
            if (PTVsubset.Count <= 1)
                return result;
            
            List<TargetPositionData> targetsData = new List<TargetPositionData>();
            foreach (var t in PTVsubset)
            {
                var mesh = t.MeshGeometry.Positions;
                double Xmin = double.PositiveInfinity, Xmax = double.NegativeInfinity;
                double Ymin = double.PositiveInfinity, Ymax = double.NegativeInfinity;
                double Zmin = double.PositiveInfinity, Zmax = double.NegativeInfinity;
                foreach (var p in mesh)
                {
                    if (p.X < Xmin) { Xmin = p.X; }
                    if (p.X > Xmax) { Xmax = p.X; }
                    if (p.Y < Ymin) { Ymin = p.Y; }
                    if (p.Y > Ymax) { Ymax = p.Y; }
                    if (p.Z < Zmin) { Zmin = p.Z; }
                    if (p.Z > Zmax) { Zmax = p.Z; }
                }
                var tpd = new TargetPositionData
                {
                    cx = t.CenterPoint,
                    Xmin = Xmin,
                    Xmax = Xmax,
                    Ymin = Ymin,
                    Ymax = Ymax,
                    Zmin = Zmin,
                    Zmax = Zmax
                };
                targetsData.Add(tpd);
            }

            // Iterate over all ordered pairs of targets, so a conflict is
            // found regardless of which target appears first in the list
            for (int i = 0; i < PTVsubset.Count; i++)
            {
                for (int j = 0; j < PTVsubset.Count; j++)
                {
                    if (i == j)
                        continue;
                    if (
                        (targetsData[i].Zmax - targetsData[j].Zmin > 100) &&
                        (targetsData[i].cx.y - targetsData[j].Ymax < 23 && targetsData[i].cx.y - targetsData[j].Ymin > -23) &&
                        (targetsData[i].cx.x - targetsData[j].Xmax < 51 && targetsData[i].cx.x - targetsData[j].Xmin > -51)
                    )
                    {
                        // Distance from top of detector plane to electornics is 116mm
                        // Add 1.6 cm here for the 50% isodose. Therefore 100mm tol.

                        // Uh oh!
                        MapCheckError e = new MapCheckError
                        {
                            QAPTV = PTVsubset[i].Id,
                            ConflictPTV = PTVsubset[j].Id
                        };
                        result.Add(e);
                    }
                }
            }
            return result;
        }



        /// <summary>
        /// Check if there exists a PTV which cannot be QA'd with MapCheck.
        /// If so, tell the RT to ask Physics advice
        /// </summary>
        public void WarnPhysics(VVector isocenter)
        {
            foreach (var e in GetMapcheckErrors(SelectedTargets))
            {
                var debugMsg = String.Format("{0} may not be able to fit on MapCheck due to position of {1}", e.QAPTV, e.ConflictPTV);
                MessageBox.Show(debugMsg, "Possible QA Issue", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
