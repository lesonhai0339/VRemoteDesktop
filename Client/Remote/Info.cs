using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class Info
    {
        public string ComputerName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
        public override string ToString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}", ComputerName, Width, Height, MajorVersion, MinorVersion);
        }
    }
}
