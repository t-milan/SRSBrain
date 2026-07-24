using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using VMS.TPS.Common.Model.Types;
using VMS.TPS.Common.Model.API;
using System.Windows.Media.Media3D;     // For MeshGeometry3D, Matrix3D, Point3D
using System.Windows;
using System.IO;

namespace Plugin
{
    static class Globals
    {
        public static int MINCOL = 5;
    }
    
    class ColResult
    {
        public int Col;
        public double IslandArea;
        public double TotalArea; // Idea is, after minimising Island Area, minimise total area. This is to take advantage of any PTV concavity. 
    }

    class BeamResult
    {
        public List<ColResult> ColResults;

        private int Sanitised(int col)
        {
            if (col > 90)
                col -= 180;
            if (col < 0)
                col += 360;
            return col;
        }

        public (int, int) OptimalCols()
        {
            // Usually for Two full arcs, we don't want same col angle.
            // Get the best two that are >= 10° from each other. 
            // We minimise the max IslandArea, then the max TotalArea
            // for Full arcs, usually end up with θ and 180-θ due to symmetry. 
            // yes, there's a slight issue where we can't have e.g. 175 and 5. TODO: fix this!

            double BestIslandArea = double.PositiveInfinity;
            double BestTotalArea = double.PositiveInfinity;

            int index1 = -1;
            int index2 = -1;

            for (int i = 0; i < ColResults.Count - 10; i++)
            {
                for (int j = i + 10; j < ColResults.Count; j++)
                {
                    double maxIslandArea = Math.Max(ColResults[i].IslandArea, ColResults[j].IslandArea);
                    double maxTotalArea = Math.Max(ColResults[i].TotalArea, ColResults[j].TotalArea);

                    if (maxIslandArea < BestIslandArea ||
                        (maxIslandArea == BestIslandArea && maxTotalArea < BestTotalArea))
                    {
                        BestIslandArea = maxIslandArea;
                        BestTotalArea = maxTotalArea;
                        index1 = i;
                        index2 = j;
                    }
                }
            }

            return (Sanitised(ColResults[index1].Col), Sanitised(ColResults[index2].Col));
        }
        
        public List<int> OptimalCols(int n)
        {
            // For 3+ arcs sharing a geometry: take angles from best to worst,
            // keeping only those >= 10 deg from every angle already chosen;
            // top up from the best remaining if the spacing runs out.
            var ranked = ColResults.OrderBy(c => c.IslandArea).ThenBy(c => c.TotalArea).ToList();
            var chosen = new List<int>();
            foreach (var c in ranked)
            {
                if (chosen.Count == n) break;
                if (chosen.All(x => Math.Abs(x - c.Col) >= 10))
                    chosen.Add(c.Col);
            }
            foreach (var c in ranked)
            {
                if (chosen.Count == n) break;
                if (!chosen.Contains(c.Col))
                    chosen.Add(c.Col);
            }
            return chosen.Select(Sanitised).ToList();
        }

        public int OptimalCol()
        {
            
            var bestCol = ColResults.OrderBy(c => c.IslandArea).ThenBy(c => c.TotalArea).FirstOrDefault();
            return Sanitised(bestCol.Col);
        }

        public int PessimalCol()
        {
            var worstCol = ColResults.OrderBy(c => c.IslandArea).ThenBy(c => c.TotalArea).LastOrDefault();
            return Sanitised(worstCol.Col);
        }
        
        public void ExportResultsToCSV(string filePath)
        {
            // Ensure that colResults is not null and filePath is a valid string
            if (ColResults == null || string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("colResults or filePath is not valid.");
            }

            // Write the contents to the CSV file
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                // Write header
                sw.WriteLine("Col,IslandArea,TotalArea");

                // Write each record
                foreach (var result in ColResults)
                {
                    sw.WriteLine($"{result.Col},{result.IslandArea},{result.TotalArea}");
                }
            }
        }
    }

    class OptimalCollimator
    {
        public Beam _beam;
        public BeamResult Result;
        public event EventHandler<int> ProgressChanged;

        public OptimalCollimator(Beam beam)
        {
            _beam = beam;
            Result = new BeamResult();
        }

        public async Task Calculate(List<Structure> selectedItems, int geomN, int geoms)
        {
            // Create dictionary of targets and their mesh geometries (IDs as keys)
            ConcurrentDictionary<String, MeshGeometry3D> MeshGeometries = new ConcurrentDictionary<String, MeshGeometry3D>();

            foreach (var item in selectedItems)
            {
                MeshGeometry3D targetMesh = item.MeshGeometry;
                targetMesh.Freeze();
                MeshGeometries.GetOrAdd(item.Id, targetMesh);
            }

            // Gets the required beam info from the plan
            ConcurrentDictionary<String, Tuple<VVector, List<Tuple<double, double>>>> beamInfo = new ConcurrentDictionary<String, Tuple<VVector, List<Tuple<double, double>>>>();
            
            VVector isoPos = _beam.IsocenterPosition;

            List<Tuple<double, double>> cpData = new List<Tuple<double, double>>();
            if (_beam.ControlPoints.Count >= 10)
            {
                foreach (ControlPoint cp in _beam.ControlPoints)
                {
                    // Get couch and gantry angles
                    double couch = cp.PatientSupportAngle;
                    double gantry = cp.GantryAngle;
                    cpData.Add(Tuple.Create(couch, gantry));
                }
            }
            else
            {
                // Manually placed (unoptimised) arcs only carry their start/stop
                // control points, so sample the swept arc at ~2 deg rather than
                // scoring just the endpoints.
                double couch = _beam.ControlPoints.First().PatientSupportAngle;
                double start = _beam.ControlPoints.First().GantryAngle;
                double stop = _beam.ControlPoints.Last().GantryAngle;
                bool clockwise = _beam.GantryDirection == GantryDirection.Clockwise;
                double span = clockwise ? (stop - start + 360) % 360 : (start - stop + 360) % 360;
                int steps = Math.Max(1, (int)Math.Round(span / 2));
                for (int i = 0; i <= steps; i++)
                {
                    double gantry = clockwise ? start + span * i / steps : start - span * i / steps;
                    gantry = (gantry % 360 + 360) % 360;
                    cpData.Add(Tuple.Create(couch, gantry));
                }
            }
            
            //Result = ColOptimiser(isoPos, cpData, MeshGeometries);
            Result = await Task.Run(() => ColOptimiser(isoPos, cpData, MeshGeometries, geomN, geoms));
        }


        private BeamResult ColOptimiser(VVector isoPos, List<Tuple<double, double>> cpData, ConcurrentDictionary<String, MeshGeometry3D> MeshGeometries, int geomN, int geoms)
        {
            // Set which collimator angles to check (e.g. range 5 - 175 inclusive, depending on MinCol)
            List<int> col_Angles = Enumerable.Range(Globals.MINCOL, 180-2*Globals.MINCOL+1).ToList();

            // Store iso in Vector3D
            Vector3D iso = new Vector3D(isoPos.x, isoPos.y, isoPos.z);

            // Creates default values for optimising (want Min optimalArea)
            double optimalArea = double.PositiveInfinity;
            double optimalColl = 0;
            var islandResults = new List<ColResult>();

            // Iterate through all collimator angles
            double colN = 1;
            int cols = col_Angles.Count;
            foreach (int coll in col_Angles)
            {
                object lockObject = new object();
                double islandArea = 0;
                double totalArea = 0;

                Parallel.ForEach(cpData, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, cp =>
                {
                    // Get couch and gantry angles
                    double couch = cp.Item1;
                    double gantry = cp.Item2;

                    // (X,Y,Z) is (B, down, G), Varian System
                    Matrix3D M_couch = CouchMatrix(couch * Math.PI / 180);
                    Matrix3D M_gantry = GantryMatrix(gantry * Math.PI / 180);
                    Matrix3D M_coll = CollMatrix(coll * Math.PI / 180);

                    // Combined transformation matrix
                    Matrix3D M_combined = M_couch * M_gantry * M_coll;

                    // MLC_Binned_Targets array of dictionaries (key = target, val = List<BEV_Pt>).
                    Dictionary<string, List<BEV_Pt>>[] MLC_Binned_Targets = new Dictionary<string, List<BEV_Pt>>[60];

                    // Intialise all elements in MLC_Binned_Targets
                    for (int i = 0; i < 60; ++i)
                    {
                        MLC_Binned_Targets[i] = new Dictionary<string, List<BEV_Pt>>();
                    }

                    foreach (var target in MeshGeometries)
                    {
                        // Target volume (TV) storing variable
                        List<BEV_Pt> TV = new List<BEV_Pt>();

                        // Transform all points in the mesh geometry
                        var transformedPoints = target.Value.Positions.Select(p => p - iso)                   // Normalise relative to isocentre position
                                                                      .Select(p => M_combined.Transform(p));  // Apply combined rotation matrices

                        foreach (Point3D p in transformedPoints)
                        {
                            // Perspective Transform (in Y Direction i.e. vertical. Source is @ -1000mm)
                            double scale_factor = 1000 / (p.Y + 1000);
                            BEV_Pt bevPoint = new BEV_Pt { x = p.X * scale_factor, y = p.Z * scale_factor };

                            // Get which MLC the point belongs to
                            int MLCno = getMLCno(bevPoint.y);

                            // If not in MLC continue
                            if (MLCno == -1) continue;

                            // Add key (target) or if already exists just add bevPoint to existing list
                            if (MLC_Binned_Targets[MLCno].ContainsKey(target.Key))
                            {
                                MLC_Binned_Targets[MLCno][target.Key].Add(bevPoint);
                            }
                            else
                            {
                                MLC_Binned_Targets[MLCno].Add(target.Key, new List<BEV_Pt>() { bevPoint });
                            }
                        }
                    }

                    (double thisIslandArea, double ThisTotalArea) = islandAreaFinder(MLC_Binned_Targets);

                    //// Concurrent adding - OLD OLD OLD
                    //Add(ref islandArea, thisIslandArea);
                    //Add(ref totalArea, ThisTotalArea);

                    lock (lockObject)
                    {
                        islandArea += thisIslandArea;
                        totalArea += ThisTotalArea;
                    }
                });

                double prog = (double)colN/cols;
                prog = (prog + geomN) / geoms * 100;
                ProgressChanged?.Invoke(this, (int)prog);
                colN += 1;

                islandResults.Add(new ColResult
                {
                    Col = coll,
                    IslandArea = islandArea,
                    TotalArea = totalArea
                });

                // Store optimal collimator angle and area
                if (islandArea < optimalArea)
                {
                    optimalArea = islandArea;
                    optimalColl = coll;
                }

            }
            
            return new BeamResult
            {
                ColResults = islandResults,
            };
        }

        private static Tuple<double,double> islandAreaFinder(Dictionary<string, List<BEV_Pt>>[] MLC_Binned_Targets)
        {
            // MLC_Binned_Targets is a list of 60 dictionaries for each MLC pair. 
            // Each dictionary maps a Target ID to a list of BEV_Pts. 
            // This function looks at each 60 MLC pairs,
            //     For each pair, look at all the targets that have a value in the dict
            //     Get the Range (max to min) for each target and add to the list of bounds
            //     Then, iterate over the ranges and find the islands
            //     Then, sum up the island areas. (probably this could be done in the previous loop. TODO: optimisation0

            double islandArea = 0;
            double totalArea = 0; //TODO - add this up!

            // Check for islands in each MLC leaf
            for (int i = 0; i < 60; i++)
            {
                // Get MLC width
                int mlc_width = 0;
                if (i < 10 || i >= 50)
                    mlc_width = 10;
                else
                    mlc_width = 5;

                double total_min = double.PositiveInfinity;
                double total_max = double.NegativeInfinity;

                // List for bounds
                List<double[]> TV_Bounds = new List<double[]>();

                foreach (var target in MLC_Binned_Targets[i])
                {
                    // Helper variables, uses infinity to ensure points within MLC leaf are captured
                    double TV_min = double.PositiveInfinity;
                    double TV_max = double.NegativeInfinity;

                    // Check every point in the TV and create bounds
                    // Note if the TV has a horseshoe shape the area it contains is not captured
                    foreach (BEV_Pt p in target.Value)
                    {
                        if (p.x < TV_min) TV_min = p.x;
                        if (p.x > TV_max) TV_max = p.x;
                    }

                    double[] newBounds = { TV_min, TV_max };
                    TV_Bounds.Add(newBounds);

                    if (TV_min < total_min) total_min = TV_min;
                    if (TV_max > total_max) total_max = TV_max;
                }

                // Sort bounds by start positions
                TV_Bounds.Sort((x, y) => x[0].CompareTo(y[0]));

                // List for islands
                List<double[]> islands = new List<double[]>();

                // Iterate over all ranges (TV_Bounds)
                for (int j = 1; j < TV_Bounds.Count; j++)
                {
                    // Previous intervals end and currents start
                    double prevEnd = TV_Bounds[j - 1][1];
                    double currStart = TV_Bounds[j][0];

                    // If end index of previous < start index of current then = free interval
                    if (prevEnd < currStart)
                    {
                        double[] island = { prevEnd, currStart };
                        islands.Add(island);
                    }
                }

                // Sum each island area
                double Area_i = 0;
                foreach (double[] island in islands)
                {
                    double len = Math.Abs(island[1] - island[0]);
                    Area_i += len * mlc_width;
                }

                islandArea += Area_i;
                if (total_max - total_min > 0) totalArea += total_max - total_min;
            }
            return Tuple.Create(islandArea, totalArea);
        }

        // Find the MLC number a value 'y' is within
        private static int getMLCno(double y)
        {
            if (y < -200)
            {
                return -1;
            }
            else if (y < -100)
            {
                return (int)(y + 200) / 10;
            }
            else if (y < 100)
            {
                return (int)(y + 100) / 5 + 10;
            }
            else if (y < 200)
            {
                return (int)(y - 100) / 10 + 50;
            }
            else
            {
                return -1;
            }
        }

        // Creation of BEV_Pt structure
        private struct BEV_Pt
        {
            public double x { get; set; }
            public double y { get; set; }
        }

        // Matrices to transform data into the BEV plane
        private static Matrix3D GantryMatrix(double g)
        {
            Matrix3D M = new Matrix3D();
            M.M11 = Math.Cos(g);
            M.M12 = -Math.Sin(g);
            M.M13 = 0;
            M.M14 = 0;

            M.M21 = Math.Sin(g);
            M.M22 = Math.Cos(g);
            M.M23 = 0;
            M.M24 = 0;

            M.M31 = 0;
            M.M32 = 0;
            M.M33 = 1;
            M.M34 = 0;

            M.OffsetX = 0;
            M.OffsetY = 0;
            M.OffsetZ = 0;

            M.M44 = 1;
            return M;
        }
        private static Matrix3D CollMatrix(double c)
        {
            Matrix3D M = new Matrix3D();
            M.M11 = Math.Cos(c);
            M.M12 = 0;
            M.M13 = -Math.Sin(c);
            M.M14 = 0;

            M.M21 = 0;
            M.M22 = 1;
            M.M23 = 0;
            M.M24 = 0;

            M.M31 = Math.Sin(c);
            M.M32 = 0;
            M.M33 = Math.Cos(c);
            M.M34 = 0;

            M.OffsetX = 0;
            M.OffsetY = 0;
            M.OffsetZ = 0;

            M.M44 = 1;
            return M;
        }
        private static Matrix3D CouchMatrix(double t)
        {
            Matrix3D M = new Matrix3D();
            M.M11 = Math.Cos(t);
            M.M12 = 0;
            M.M13 = Math.Sin(t);
            M.M14 = 0;

            M.M21 = 0;
            M.M22 = 1;
            M.M23 = 0;
            M.M24 = 0;

            M.M31 = -Math.Sin(t);
            M.M32 = 0;
            M.M33 = Math.Cos(t);
            M.M34 = 0;

            M.OffsetX = 0;
            M.OffsetY = 0;
            M.OffsetZ = 0;

            M.M44 = 1;
            return M;
        }
        public static double Add(ref double location1, double value)
        {
            double newCurrentValue = location1; // non-volatile read, so may be stale
            while (true)
            {
                double currentValue = newCurrentValue;
                double newValue = currentValue + value;
                newCurrentValue = Interlocked.CompareExchange(ref location1, newValue, currentValue);
                if (newCurrentValue.Equals(currentValue)) // see "Update" below
                    return newValue;
            }
        }
    }
}
