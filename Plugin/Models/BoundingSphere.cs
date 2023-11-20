using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics.LinearAlgebra;
using System.Numerics;
using VMS.TPS.Common.Model.API;
using SEB;

//// TESTING CIRCUMSPHERE THING
//// Initialize a list of points
//var points = new List<Vector<double>>
//            {
//                Vector<double>.Build.DenseOfArray(new double[] {0, 0}),
//                Vector<double>.Build.DenseOfArray(new double[] {1, 0}),
//                Vector<double>.Build.DenseOfArray(new double[] {0, 1})
//            };

//// Convert the list of points to a matrix
//var matrix = Matrix<double>.Build.DenseOfRowVectors(points);

//// Initialize a CircumsphereSolver object
//var solver = new CircumsphereSolver();

//// Call the GetCircumsphere function
//var (center, squaredRadius) = CircumsphereSolver.GetCircumsphere(matrix);

//// Print the center and squared radius of the circumsphere
//MessageBox.Show($"Center: {center}");
//MessageBox.Show($"Squared radius: {squaredRadius}");

namespace Plugin.Models
{

    public class BoundingSphere
    {
        public double X;
        public double Y;
        public double Z;
        public double Radius;

        public static BoundingSphere SphereFromTargets(List<Structure> selectedTargets)
        {
            int num = 0;
            foreach (var s in selectedTargets)
            {
                num += s.MeshGeometry.Positions.Count;
            }

            ArrayPointSet pts = new ArrayPointSet(3, num);
            int i = 0;
            foreach (var s in selectedTargets)
            {
                var mesh = s.MeshGeometry;
                foreach (var p in mesh.Positions)
                {
                    pts.Set(i, 0, p.X);
                    pts.Set(i, 1, p.Y);
                    pts.Set(i, 2, p.Z);
                    i++;
                }
            }

            var mb = new Miniball(pts);

            return new BoundingSphere { X = mb.Center[0], Y = mb.Center[1], Z = mb.Center[2], Radius = mb.Radius };
        }
    }
}
