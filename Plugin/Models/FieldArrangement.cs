using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;

namespace Plugin.Models
{
    public enum ArcLen
    {
        Full,
        Half,
    }

    public class SimpleBeam
    {
        public ArcLen arcLen;
        public double gStart;
        public double gStop;
        public GantryDirection gDir;
        public double couch;
        public string Id;

        public SimpleBeam(ArcLen arcLen, double gStart, double gStop, GantryDirection gDir, double couch, string id)
        {
            this.arcLen = arcLen;
            this.gStart = gStart;
            this.gStop = gStop;
            this.gDir = gDir;
            this.couch = couch;
            this.Id = id;
        }
    }

    // Which half of the gantry circle a half arc sweeps: through gantry 90
    // (0 <-> 179.9) or through gantry 270 (180.1 <-> 0).
    public enum ArcSide
    {
        Via90,
        Via270,
    }

    public class HalfArc
    {
        public double Couch; // IEC 61217, as used by ESAPI
        public ArcSide Side;

        public HalfArc(double couch, ArcSide side)
        {
            Couch = couch;
            Side = side;
        }
    }

    public static class FieldSequencer
    {
        // With the gantry within this many degrees of 0 or 180 it clears the
        // couch at any couch angle.
        private const double CouchClearance = 10;

        /// <summary>
        /// Picks the delivery order and direction of a set of half arcs. Each
        /// slot is one field; a slot with several options (the vertex field,
        /// which gives the same beam directions at couch 90 or 270) lets the
        /// sequencer pick the one that fits best.
        ///
        /// The first field starts at gantry 180 (where the CBCT leaves the
        /// gantry, with the couch at 0). The order then minimises couch plus
        /// gantry travel between fields, rejecting any gantry move that would
        /// swing through the couch (e.g. 179 -> 0 via 90 at couch 315), and
        /// breaks ties on fewer couch moves, then the first vertex option,
        /// then template order.
        /// </summary>
        public static List<SimpleBeam> Sequence(List<List<HalfArc>> slots)
        {
            int n = slots.Count;
            double[] bestKey = null;
            List<SimpleBeam> best = new List<SimpleBeam>();

            foreach (var choice in Choices(slots, 0, new int[n]))
            {
                foreach (var perm in Permutations(n))
                {
                    // Bit k set: the k-th field runs from gantry 0 to 180,
                    // otherwise from 180 to 0. The first field always starts at 180.
                    for (int dirs = 0; dirs < (1 << n); dirs += 2)
                    {
                        double couch = 0;
                        double gantry = 0;
                        double cost = 0;
                        int couchMoves = 0;
                        bool collides = false;
                        for (int k = 0; k < n && !collides; k++)
                        {
                            var arc = slots[perm[k]][choice[perm[k]]];
                            bool fromZero = ((dirs >> k) & 1) == 1;
                            double start = fromZero ? 0 : Near180(arc.Side);
                            double stop = fromZero ? Near180(arc.Side) : 0;

                            if (k > 0)
                            {
                                double lo = Math.Min(gantry, start);
                                double hi = Math.Max(gantry, start);
                                // Safe whether the gantry is moved before or after the couch
                                collides = Collides(couch, lo, hi) || Collides(arc.Couch, lo, hi);
                                cost += hi - lo;
                            }

                            double couchMove = CouchDistance(couch, arc.Couch);
                            if (couchMove > 0)
                                couchMoves++;
                            cost += couchMove;

                            couch = arc.Couch;
                            gantry = stop;
                        }
                        if (collides)
                            continue;

                        var key = new List<double> { Math.Round(cost, 1), couchMoves };
                        key.AddRange(choice.Select(c => (double)c));
                        key.AddRange(perm.Select(p => (double)p));
                        key.Add(dirs);
                        if (bestKey == null || CompareKeys(key, bestKey) < 0)
                        {
                            bestKey = key.ToArray();
                            best = BuildBeams(slots, choice, perm, dirs);
                        }
                    }
                }
            }
            return best;
        }

        // Gantry positions are handled on the machine's linear scale, which
        // runs 180.1 (-179.9) through 0 to 179.9 without crossing 180, so a
        // move sweeps the interval between its end points.
        private static double Near180(ArcSide side) => side == ArcSide.Via90 ? 179.9 : -179.9;

        private static double ToGantryAngle(double linear) => linear < 0 ? linear + 360 : linear;

        private static bool Collides(double couch, double lo, double hi)
        {
            double c = ((couch % 360) + 360) % 360;
            if (c == 0)
                return false;
            // Couch kicked so the patient's feet swing towards the gantry 90
            // side: only the 180.1 <-> 0 half is clear (and vice versa).
            if (c < 180)
                return hi > CouchClearance && lo < 180 - CouchClearance;
            return lo < -CouchClearance && hi > -(180 - CouchClearance);
        }

        private static double CouchDistance(double a, double b)
        {
            double d = Math.Abs(a - b) % 360;
            return Math.Min(d, 360 - d);
        }

        private static int CompareKeys(List<double> a, double[] b)
        {
            for (int i = 0; i < a.Count; i++)
            {
                int c = a[i].CompareTo(b[i]);
                if (c != 0)
                    return c;
            }
            return 0;
        }

        private static List<SimpleBeam> BuildBeams(List<List<HalfArc>> slots, int[] choice, int[] perm, int dirs)
        {
            var beams = new List<SimpleBeam>();
            for (int k = 0; k < perm.Length; k++)
            {
                var arc = slots[perm[k]][choice[perm[k]]];
                bool fromZero = ((dirs >> k) & 1) == 1;
                double near180 = Near180(arc.Side);
                double start = ToGantryAngle(fromZero ? 0 : near180);
                double stop = ToGantryAngle(fromZero ? near180 : 0);
                // Clockwise means increasing gantry angle: 0 -> 179.9 or 180.1 -> 0 (via 360)
                bool clockwise = (arc.Side == ArcSide.Via90) == fromZero;
                // Field IDs number the delivery order and give the couch on
                // the Varian scale shown to the RTs (360 - IEC).
                int displayCouch = (int)Math.Round((360 - arc.Couch) % 360);
                beams.Add(new SimpleBeam(ArcLen.Half, start, stop,
                    clockwise ? GantryDirection.Clockwise : GantryDirection.CounterClockwise,
                    arc.Couch, $"{k + 1:00}_T{displayCouch}"));
            }
            return beams;
        }

        private static IEnumerable<int[]> Choices(List<List<HalfArc>> slots, int i, int[] current)
        {
            if (i == slots.Count)
            {
                yield return (int[])current.Clone();
                yield break;
            }
            for (int o = 0; o < slots[i].Count; o++)
            {
                current[i] = o;
                foreach (var c in Choices(slots, i + 1, current))
                    yield return c;
            }
        }

        private static IEnumerable<int[]> Permutations(int n)
        {
            return Permute(Enumerable.Range(0, n).ToList());
        }

        private static IEnumerable<int[]> Permute(List<int> remaining)
        {
            if (remaining.Count == 0)
            {
                yield return new int[0];
                yield break;
            }
            foreach (int first in remaining)
            {
                var rest = remaining.Where(r => r != first).ToList();
                foreach (var tail in Permute(rest))
                    yield return new[] { first }.Concat(tail).ToArray();
            }
        }
    }
}



