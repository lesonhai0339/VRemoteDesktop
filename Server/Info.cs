using RemoteServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Info
    {
        public string Id { get; set; }
        public string Password { get; set; }
        public string ComputerName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }
        public Client Client { get; set; }
        public override string ToString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion);
        }
    }
}
