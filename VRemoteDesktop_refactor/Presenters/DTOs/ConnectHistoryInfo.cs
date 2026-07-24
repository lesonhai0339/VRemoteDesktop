using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.DTOs
{
    public class ConnectHistoryInfo
    {
        public ConnectHistoryInfo() { }
        public ConnectHistoryInfo(string id, string name, string lastConnect)
        {
            Id = id;
            Name = name;
            LastConnect = lastConnect;
        }
        public string Id { get; set; }
        public string Name { get; set; }
        public string LastConnect { get; set; }
    }
}
