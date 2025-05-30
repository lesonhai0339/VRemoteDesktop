using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Connection
    {
        public Connection()
        {

        }
        public Connection(string sessionId, Client client1= null, string ip1= "", int port1=0, Client client2 = null , string ip2= "", int port2=0)
        {
            SessionId = sessionId;
            Client1 = client1;
            ClientIP1 = ip1;
            ClientPort1 = port1;
            Client2 = client2;
            ClientIP2 = ip2;
            ClientPort2 = port2;
        }
        public string SessionId { get; set; }   
        public string ClientIP1 { get; set; }
        public int ClientPort1 { get; set; }
        public Client Client1 { get; set; }

        public string ClientIP2 { get; set; }
        public int ClientPort2 { get; set; }
        public Client Client2 { get; set; }
    }
}
