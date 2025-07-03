using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Utils;

namespace VRemoteServer.Models
{
    public class RemoteTask
    {
        public Enums.CommandType CommandType { get; set; }
        public Client Client { get; set; }
        public byte[] Data { get; set; }
    }
}
