using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.Utils;

namespace VRemoteServer.Services
{
    public class AuthenticationService
    {
        public AuthenticationService()
        {

        }
        public bool ValidateLogin(RemoteTask task)
        {
            try
            {
                byte[] data = new byte[task.Data.Length - 13];
                Buffer.BlockCopy(task.Data, 13, data, 0, task.Data.Length - 13);

                IPEndPoint ep = task.Client.Socket.RemoteEndPoint as IPEndPoint;

                var clientInfo = Encoding.ASCII.GetString(data).Replace(" ", "").Split('|');
                if (clientInfo.Length != 7)
                {
                    return false;
                }

                var isNullOrEmpty = clientInfo.All(x => x != null);
                if (!isNullOrEmpty)
                    return false;
                if (clientInfo[0].Length != 8)
                    return false;
                if (clientInfo[1].Length != 4)
                    return false;

                ClientInfo loginInfo = new ClientInfo
                {
                    Id = clientInfo[0],
                    Password = clientInfo[1],
                    ComputerName = clientInfo[2],
                    Width = int.Parse(clientInfo[3]),
                    Height = int.Parse(clientInfo[4]),
                    MajorVersion = clientInfo[5],
                    MinorVersion = clientInfo[6],
                    Ip = ep.Address.ToString(),
                    PublicIP = ep.Address.ToString(),
                    Port = ep.Port.ToString(),
                    Client = task.Client
                };
            }
            catch (Exception ex)
            {
            }
        }

    }
}
