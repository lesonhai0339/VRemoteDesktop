using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class ConnectionInfo
    {
        public ConnectionInfo() { }
        public ConnectionInfo(string sessionId, Info partner)
        {
            SessionId = sessionId;
            PartnerInfo = partner;

        }
        public string SessionId { get; set; }
        public Info PartnerInfo { get; set; }
    }
    public class Info
    {
        public string Id { get; set; }
        public string Password { get; set; }
        public string ComputerName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
        public override string ToString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion);
        }
    }
}
