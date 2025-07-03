using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Models;

namespace VRemoteServer.Services
{
    internal class RemoteDesktopServer
    {

        public static async Task<bool> ProcessDataCallback(Client client, byte[] buffer, int length)
        {
            return true;
        }
        public static void ClientDisconnectCallback(Client client)
        {

        }
    }
}
