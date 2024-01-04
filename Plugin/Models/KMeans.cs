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

            // Create a new K-Means algorithm with 3 clusters 
            // Could also just do Accord.MachineLearning.KMeans kmeans = new KMeans(k: numClusters);
            Accord.MachineLearning.KMeans kmeans = new Accord.MachineLearning.KMeans(k: numClusters);
            //UseSeeding = Accord.MachineLearning.Seeding.PamBuild // Algorithm is KMeansPlusPlus by default. 

            // Compute and retrieve the data centroids
            var clusters = kmeans.Learn(observations);

            // Use the centroids to parition all the data
            labels = clusters.Decide(observations);

            // Creating the dictionary
            clusterDictionary = new Dictionary<int, List<string>>();

            for (int i = 0; i < labels.Length; i++)
            {
                // Check if the cluster label is already a key in the dictionary
                if (!clusterDictionary.ContainsKey(labels[i]))
                {
                    clusterDictionary[labels[i]] = new List<string>();
                }

                // Add the observation to the corresponding cluster
                clusterDictionary[labels[i]].Add(names[i]);
            }
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
