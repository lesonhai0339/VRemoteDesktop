using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.Models
{
    public class ChatConnection
    {
        public ChatConnection(VClient client, List<object> lists)
        {
            Client = client;
            Messages = lists;
        }
        public VClient Client { get; set; }
        public List<object> Messages { get; set; }
    }
}
