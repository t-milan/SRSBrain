using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.Types;

namespace Plugin.Models
{
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

    public class FieldArrangement
    {
        public string DisplayName { get; set; }
        public string ImagePath { get; set; }
        public List<SimpleBeam> Geometry() {
            List<SimpleBeam> beams = new List<SimpleBeam>();
            if (DisplayName == "Existing Geometry")
            {

            }
            else if (DisplayName == "A: 2×Half, 1×[45,90,315]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Half, 180.1, 0,     GantryDirection.Clockwise,        0,   "01_T0"),
                    new SimpleBeam(ArcLen.Half, 0, 179.9,     GantryDirection.Clockwise,        0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 315, "03_T45"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        270, "04_T90"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 45,  "05_T315"),
                };
            }
            else if (DisplayName == "B: 2×Full, 1×[45,90,315]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Full, 179.9, 180.1, GantryDirection.CounterClockwise, 0,   "01_T0"),
                    new SimpleBeam(ArcLen.Full, 180.1, 179.9, GantryDirection.Clockwise,        0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 315, "03_T45"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        270, "04_T90"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 45,  "05_T315"),
                };
            }
            else if (DisplayName == "C: 2×Full, 1×[60,300]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Full, 179.9, 180.1, GantryDirection.CounterClockwise, 0,   "01_T0"),
                    new SimpleBeam(ArcLen.Full, 180.1, 179.9, GantryDirection.Clockwise,        0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 300, "03_T60"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 60,  "04_T300"),
                };
            }
            else if (DisplayName == "D: 2×Full, 2×[60,300]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Full, 180.1, 179.9, GantryDirection.Clockwise,        0,   "01_T0"),
                    new SimpleBeam(ArcLen.Full, 179.9, 180.1, GantryDirection.CounterClockwise, 0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        300, "03_T60"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 300, "04_T60"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 60,  "05_T300"),
                    new SimpleBeam(ArcLen.Half, 180.1, 0,     GantryDirection.Clockwise,        60,  "06_T300"),
                };
            }
            else if (DisplayName == "E: 2×Half, 1×[60,300]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Half, 180.1, 0,     GantryDirection.Clockwise,        0,   "01_T0"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 300, "03_T60"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 60,  "04_T300"),
                };
            }
            else if (DisplayName == "F: 2×Half, 2×[60,300]")
            {
                beams = new List<SimpleBeam>
                {
                    new SimpleBeam(ArcLen.Half, 180.1, 0,     GantryDirection.Clockwise,        0,   "01_T0"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        0,   "02_T0"),
                    new SimpleBeam(ArcLen.Half, 0,     179.9, GantryDirection.Clockwise,        300, "03_T60"),
                    new SimpleBeam(ArcLen.Half, 179.9, 0,     GantryDirection.CounterClockwise, 300, "04_T60"),
                    new SimpleBeam(ArcLen.Half, 0,     180.1, GantryDirection.CounterClockwise, 60,  "05_T300"),
                    new SimpleBeam(ArcLen.Half, 180.1, 0,     GantryDirection.Clockwise,        60,  "06_T300"),
                };
            }
            else
            {
                throw new Exception("Wrong Field Geometry Selected!");
            }
            return beams;
        }
    }

    public enum ArcLen
    {
        Full,
        Half,
    }

    
}



