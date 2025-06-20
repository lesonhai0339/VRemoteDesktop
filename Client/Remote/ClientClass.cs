using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class ClientClass
    {
        private Timer _timer;
        private SocketRemoteClient _remoteClient;
        private ConnectionInfo _connectionInfo;
        private ManualResetEvent _resetEvent;
        public ClientClass(SocketRemoteClient remoteCLient, ConnectionInfo info)
        {
            Client = remoteCLient;
            ResetEvent = new ManualResetEvent(false);
            _connectionInfo = info;
            _timer = new Timer(SendScreen, null, 0, 1000);
        }
        #region Properties
        public SocketRemoteClient Client
        {
            get=> _remoteClient;
            set
            {
                if(_remoteClient != null)
                {
                    _remoteClient.P2PDataSendSuccessEventHandler -= P2PDataSendSuccess;
                }
                _remoteClient = value;
                if(_remoteClient != null)
                {
                    _remoteClient.P2PDataSendSuccessEventHandler += P2PDataSendSuccess;
                }
            }
        }
        public ManualResetEvent ResetEvent
        {
            get=> _resetEvent;
            private set
            {
                _resetEvent = value;
            }
        }
        #endregion
        #region Functions
        private void SendScreen(object state)
        {
            var x = CaptureScreen.GetScreen();
            if (x.Any())
            {
                Send(Enums.DataType.STARTSCREEN, new byte[] { });
                Console.WriteLine(Math.Min(4096, x[0].Bytes.Length));
                Send(Enums.DataType.P2PDATASEND, x[0].Bytes);
                Send(Enums.DataType.ENDSCREEN, new byte[] { });
            }
        }
        private bool Send(Enums.DataType type, byte[] data, int timeout = 5)
        {
            ResetEvent.Reset();
            _remoteClient.Send(type, data);
            var flag = ResetEvent.WaitOne(1000 * timeout);
            if (flag)
            {
                return true;
            }
            return false;
        }
        #region Events
        private void P2PDataSendSuccess()
        {
            ResetEvent.Set();
        }
        #endregion
        #endregion
    }
}
