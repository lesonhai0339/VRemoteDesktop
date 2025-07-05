using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VRemoteClient.Models.;
using VRemoteClient.Models.Entities;

namespace VRemoteClient.Services
{
    public class RemoteClient : IDisposable
    {
        private const string REMOTE_SERVER_IP = "";
        private const int REMOTE_SERVER_PORT = 2399;
        private const int MAX_BUFFER_SIZE = 10 * 1024 * 1024;

        private bool _isSocketConnected;
        private bool _isP2PConnected;
        private bool _isDisposed;

        private Socket _socket;
        private Timer _timer;

        public delegate void ConnectEvent();
        public delegate void LoginEvent();
        public delegate void P2PConnectEvent();
        public delegate void P2PDataSendSuccessEvent();
        public delegate void P2PScreenEvent(byte[] screen);

        public event ConnectEvent ConnectedEventHandler;
        public event LoginEvent LoginEventHandler;
        public event P2PConnectEvent P2PConnectEventHandler;
        public event P2PDataSendSuccessEvent P2PDataSendSuccessEventHandler;
        public event P2PScreenEvent P2PScreenEventHandler;

        CancellationTokenSource _cancellationToken;

        public RemoteClient()
        {
            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cancellationToken = new CancellationTokenSource();
        }
        #region Properties
        public Socket Socket
        {
            get => _socket;
            private set
            {
                //if (_socket != null)
                //{
                //    _socket.Close();
                //    _socket.Dispose();
                //}
                _socket = value;
            }
        }
        public bool SocketConnected
        {
            get => _isSocketConnected;
            private set
            {
                _isSocketConnected = value;
            }
        }
        #endregion
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }
        public void Connect(string ip = REMOTE_SERVER_IP, int port = REMOTE_SERVER_PORT)
        {
            try
            {
                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out IPAddress _))
                {
                    remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);

                    if(Socket == null)
                    {
                        Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        Socket.NoDelay = true;
                    }
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    Socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Socket);
                }
                else
                {
                    Log.Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Log.Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error when connect to relay server");
            }
            finally
            {

            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                Socket.EndConnect(ar);
                if (Socket.Connected)
                {
                    SocketConnected = true;
                }
                ConnectEvent connectEvent = ConnectedEventHandler;
                if (connectEvent != null)
                {
                    connectEvent();
                }
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;


                Socket.BeginReceive(stateObject.Buffer, 0, 1024, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);

            }
            catch(SocketException ex)
            {
                Log.Error(ex, "SocketException when connecting to remote server");
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Unexpected error when connecting to remote server");
            }
        }

        private void DataCallback(IAsyncResult ar)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
