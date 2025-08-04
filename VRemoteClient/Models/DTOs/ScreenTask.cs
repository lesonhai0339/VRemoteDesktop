using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.Entities
{
    public class ScreenTask
    {

        public ScreenEnum WorkType { get; set; }
        public List<ScreenRegion> Blocks { get; set; }
        public int TotalSize { get; set; }
    }
}
