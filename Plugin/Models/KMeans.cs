using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Plugin.Models
{


    public class Point3D
    {
        public double X, Y, Z;

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public static class KMeans
    {
        public static List<Point3D> Cluster(List<Point3D> points, int numClusters, int maxIterations)
        {
            // Initialize centroids randomly
            Random random = new Random();
            List<Point3D> centroids = points.OrderBy(x => random.Next()).Take(numClusters).ToList();

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                List<List<Point3D>> clusters = new List<List<Point3D>>();

                for (int i = 0; i < numClusters; i++)
                {
                    clusters.Add(new List<Point3D>());
                }

                foreach (Point3D point in points)
                {
                    int closestCentroidIndex = GetClosestCentroidIndex(point, centroids);
                    clusters[closestCentroidIndex].Add(point);
                }

                // Update centroids
                for (int i = 0; i < numClusters; i++)
                {
                    if (clusters[i].Count > 0)
                    {
                        centroids[i] = GetCentroid(clusters[i]);
                    }
                }
            }

            return centroids;
        }

        private static int GetClosestCentroidIndex(Point3D point, List<Point3D> centroids)
        {
            int closestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < centroids.Count; i++)
            {
                double distance = GetDistance(point, centroids[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private static double GetDistance(Point3D a, Point3D b)
        {
            return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
        }

        private static Point3D GetCentroid(List<Point3D> points)
        {
            double sumX = 0, sumY = 0, sumZ = 0;
            int count = points.Count;

            foreach (Point3D point in points)
            {
                sumX += point.X;
                sumY += point.Y;
                sumZ += point.Z;
            }

            return new Point3D(sumX / count, sumY / count, sumZ / count);
        }
    }

    //public class MyProgram
    //{
    //    public static void Main(string[] args)
    //    {
    //        List<Point3D> points = new List<Point3D>
    //    {
    //        new Point3D(1, 1, 1),
    //        new Point3D(2, 2, 2),
    //        new Point3D(3, 3, 3),
    //        new Point3D(10, 10, 10),
    //        new Point3D(11, 11, 11),
    //        new Point3D(14, 14, 10)
    //    };

    //        int numClusters = 2;
    //        int maxIterations = 100;

    //        List<Point3D> centroids = KMeans.Cluster(points, numClusters, maxIterations);

    //        Console.WriteLine("Centroids:");
    //        foreach (Point3D centroid in centroids)
    //        {
    //            Console.WriteLine($"({centroid.X}, {centroid.Y}, {centroid.Z})");
    //        }
    //    }
    //}
}
