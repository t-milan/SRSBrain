using SEB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using System.Windows.Media.Media3D;
using System.Windows;

using System.Security.Policy;
using System.Xml.Linq;
using Accord.MachineLearning;

namespace Plugin.Models
{


    public class Pt3D
    {
        public double X, Y, Z;

        public Pt3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class KMeans
    {

        // Declare member variables
        private double[][] observations;
        private string[] names;
        private int[] labels;
        public Dictionary<int, List<string>> clusterDictionary;
        // Dictionary is like {0: ["PTV1", "PTV3"], 1: ["PTV2"]}

        private static Pt3D GetCentroidOfStructure(Point3DCollection point3Ds)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            int count = point3Ds.Count;

            foreach (var point in point3Ds)
            {
                sumX += point.X;
                sumY += point.Y;
                sumZ += point.Z;
            }

            return new Pt3D(sumX / count, sumY / count, sumZ / count);
        }
        
        // Constructor
        public KMeans(List<Structure> selectedTargets, int numClusters)
        {
            observations = new double[selectedTargets.Count][];
            names = new string[selectedTargets.Count];

            for (int i = 0; i < selectedTargets.Count; i++)
            {
                var c = GetCentroidOfStructure(selectedTargets[i].MeshGeometry.Positions);
                
                observations[i] = new double[] { c.X, c.Y, c.Z };
                names[i] = selectedTargets[i].Id;
            }

            int numIterations = 10; // Number of times to run K-means
            double bestError = double.MaxValue;
            KMeansClusterCollection bestClusters = null;
            int[] bestLabels = null;

            for (int iteration = 0; iteration < numIterations; iteration++)
            {
                Accord.MachineLearning.KMeans kmeans = new Accord.MachineLearning.KMeans(k: numClusters) { 
                    Distance = 
                };

                var clusters = kmeans.Learn(observations);
                var labels = clusters.Decide(observations);

                // Calculate the total within-cluster sum of squares for this solution
                double error = CalculateTotalWithinClusterVariance(observations, labels, clusters);

                // If this solution is better than what we've seen so far, keep it
                if (error < bestError)
                {
                    bestError = error;
                    bestClusters = clusters;
                    bestLabels = labels;
                }
            }

            // Creating the dictionary
            clusterDictionary = new Dictionary<int, List<string>>();

            for (int i = 0; i < bestLabels.Length; i++)
            {
                // Check if the cluster label is already a key in the dictionary
                if (!clusterDictionary.ContainsKey(bestLabels[i]))
                {
                    clusterDictionary[bestLabels[i]] = new List<string>();
                }

                // Add the observation to the corresponding cluster
                clusterDictionary[bestLabels[i]].Add(names[i]);
            }

            // Sort them here?
        }

        // You'll need to implement this method to calculate the within-cluster variance
        double CalculateTotalWithinClusterVariance(double[][] observations, int[] labels, KMeansClusterCollection clusters)
        {
            double totalVariance = 0;
            for (int i = 0; i < observations.Length; i++)
            {
                int clusterIndex = labels[i];
                var clusterCenter = clusters[clusterIndex].Centroid;
                totalVariance += SquareDistance(observations[i], clusterCenter);
            }
            return totalVariance;
        }

        // Implement a squared distance function
        double SquareDistance(double[] point, double[] center)
        {
            double distance = 0;
            for (int i = 0; i < point.Length; i++)
            {
                distance += Math.Pow(point[i] - center[i], 2);
            }
            return distance;
        }

        public void WriteMessageOld()
        {
            string myMessage = "";
            for (int i = 0; i < labels.Length; i++)
            {
                myMessage += $"{names[i]}: (Cluster {labels[i]}): " +
                             $"\nCoordinates:({observations[i][0].ToString("F2")}, " +
                             $"{observations[i][1].ToString("F2")}, " +
                             $"{observations[i][2].ToString("F2")})\n\n";
            }
            MessageBox.Show(myMessage);
        }

        public void WriteMessage()
        {
            var stringBuilder = new StringBuilder();

            foreach (var pair in clusterDictionary)
            {
                stringBuilder.AppendLine($"Isocentre {pair.Key}:");
                foreach (var ptv in pair.Value)
                    stringBuilder.AppendLine($"    {ptv}");
                stringBuilder.AppendLine();
            }

            // Convert the StringBuilder to a string
            string myMessage = stringBuilder.ToString();
            MessageBox.Show(myMessage);
        }
    }




}
