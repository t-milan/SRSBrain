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
    
}



