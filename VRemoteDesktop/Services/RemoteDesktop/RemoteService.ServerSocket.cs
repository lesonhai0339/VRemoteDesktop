using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
    {
        #region ClientSession
        #region Server
        #region Properties
        #endregion
        #region Methods
        public ClientSession NewSocketServer(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                sessionId = StringHelper.RandomString(SESSION_ID_LENGTH);

            return _sessionManager.New(sessionId, SessionManagement.Enums.ClientType.System);
        }
        public void ServerSocketLogin(ClientSession client)
        {
            if (client == null)
                throw new ArgumentNullException("ClientSession cannot be null");

            var machineInfo = GetMachineInfo();
            var result = ByteArrayHelper.ConvertStringToByteArray(machineInfo.ToNetworkString(), Enums.EncodingType.ASCII);
            if (result.IsSuccess)
            {
                client.Send(Models.SocketDataType.Login, result.Data);
            }
        }
        public void ServerSocketListen(ClientSession client, int port)
        {
            if (client == null)
                throw new ArgumentNullException("Client session cannot be null");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("Port cannot less than or equal zero");

            client.Listen(port: port);
        }
        public void GetPartnerInfo(ClientSession client, string id, string password)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("Partner id cannot be null or empty");
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException("Partner password cannot be null or empty");

            var partnerInfo = StringHelper.StringBuilderWithSeparator(SEPARATOR, id, password);
            var result = ByteArrayHelper.ConvertStringToByteArray(partnerInfo, Enums.EncodingType.ASCII);
            if (result.IsSuccess)
            {
                client.Send(SocketDataType.GetPartnerInfo, result.Data);
            }
        }
        #endregion
        #region Events
        private void LoginEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            try
            {
                //Server respond public ip
                string[] respond = Encoding.ASCII.GetString(e.Data).Split('|');
                if (respond[0] == SUCCESS)
                {
                    UpdatePublicIp(respond[1]);
                    RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(SocketDataType.Login, true));
                }
                else
                {
                    RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(SocketDataType.LoginFailed, false));
                }
            }
            catch(Exception ex)
            {
                throw;
            }
        }
        #endregion
        #endregion
        #endregion
    }
}
