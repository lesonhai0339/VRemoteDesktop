//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace VRemoteServer.Models
//{
//    public class ConnectionInfo
//    {
//        public ConnectionInfo(string connectionId, Client sender)
//        {
//            ConnectionId = connectionId;
//            Sender = sender;
//        }
//        public ConnectionInfo(Client sender, Client receiver)
//        {
//            Sender = sender;
//            Receiver = receiver;
//        }
//        public ConnectionInfo(string connectionId, Client sender, Client receiver)
//        {
//            ConnectionId = connectionId;
//            Sender = sender;
//            Receiver = receiver;
//        }
//        public string ConnectionId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 16);
//        public Client Sender { get; set; }
//        public Client Receiver { get; set; }
//    }
//}
