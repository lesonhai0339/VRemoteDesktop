using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.RemoteClientService;
using VRemoteClient.Services.RemoteDesktopService;
using VRemoteClient.Services.SessionManagerment;

namespace VRemoteClient.Services
{
    public class MainController
    {
        private FormManagement<Form> _formmanager;
        private RemoteDesktop _remoteDesktop;
        private RemoteClient _remoteClient;
        private ClientInfo _me;
        private Dictionary<SocketDataType, Action<object>> _commands;
        public MainController()
        {
            InitializeConponent();
        }
        private void InitializeConponent()
        {
            _me = ConnectionService.ConnectionManagerment.Me;
            _formmanager = new FormManagement<Form>();
            _remoteDesktop = new RemoteDesktop();
            _remoteClient = new RemoteClient(_me);
            _commands = InitCommands();
        }
        private Dictionary<SocketDataType, Action<object>> InitCommands()
        {
            Dictionary<SocketDataType, Action<object>> commands = new Dictionary<SocketDataType, Action<object>>()
            {
                { SocketDataType.Clipboard, data => ClipboardHandler(data) },
            };

            return commands;
        }
        private void ClipboardHandler(object data)
        {
            byte[] clipboard = _remoteDesktop.GetClipboardByteArray();
            _remoteClient.Send(clipboard);
        }
    }
}
