using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;

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

        public delegate void ConnectSckEvent();
        public delegate void LoginEvent();
        public delegate void P2PConnectEvent();
        public delegate void P2PDataSendSuccessEvent();
        public delegate void P2PScreenEvent(byte[] screen);

        public event ConnectSckEvent ConnectSckEventHandler;
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
                ConnectSckEvent connectEvent = ConnectSckEventHandler;
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
            try
            {
                StateObject stateObject = (StateObject)ar.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(ar);
                if(num > 0)
                {
                    stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0, num);
                    while (!_cancellationToken.Token.IsCancellationRequested)
                    {
                        if(!(stateObject.ByteArrayBuilder.Length >= 4))
                        {
                            break;
                        }
                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, 4).ToArray(), 0);

                        if(!(stateObject.ByteArrayBuilder.Length >= length + 4))
                        {
                            break;
                        }

                        Array src = stateObject.ByteArrayBuilder.Cut(length + 4).ToArray();
                        byte[] data = new byte[length];
                        Buffer.BlockCopy(src , 4, data, 0 , length);
                        ProcessReceiveData(data);
                        if (_cancellationToken.IsCancellationRequested) break;

                    }
                }
                try
                {
                    Socket.BeginReceive(stateObject.Buffer, 0, stateObject.Buffer.Length, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);  
                }
                catch
                {
                    //Socket.Close();
                }
            }
            catch(SocketException ex)
            {
                Log.Error(ex, "SocketException when receiving data from remote server");    
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Unexpected error when receiving data from remote server");
            }
        }

        private void ProcessReceiveData(byte[] data)
        {
            CommandType commandType = (CommandType)data[0];
            switch (commandType)
            {
                case CommandType.Login:
                    LoginEvent loginEvent = LoginEventHandler;
                    if(loginEvent != null)
                    {
                        loginEvent();
                    }
                    break;
                case CommandType.P2PConnect:
                    break;
                case CommandType.Disconnect:
                    break;
                case CommandType.Data:
                    break;
                case CommandType.Ping:
                    break;
                case CommandType.Pong:
                    break;
                case CommandType.Error:
                    break;
                case CommandType.LoginFailed:
                    break;
                case CommandType.PartnerDisconnected:
                    break;
                case CommandType.P2PConnectFailed:
                    break;
                default:
                    break;
            }
        }
        public void Send(CommandType commandType, byte[] data)
        {
            try
            {
                //send header before send data
                byte[] header = new byte[5];
                Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, header, 0, 4);
                header[4] = (byte)commandType; //set command type
                Socket.BeginSend(header, 0, header.Length, SocketFlags.None, null, null);

                //send data
                Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when sending data to remote server");
            }
        }
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
                _socket?.Close();
                _isDisposed = true;
            }
        }
    }
}
