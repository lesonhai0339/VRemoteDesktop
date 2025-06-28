using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public class SocketRemoteClient: IDisposable
    {
        private const int MAX_BUFFER_SIZE = 10 * 1024 * 1024;
        private bool _disposed = false;
        private bool _isSocketConnected;
        private bool _isP2PConnected;
        private Socket _socket;
        private System.Threading.Timer _pingTimer;


        public delegate void ConnectedEvent();
        public event ConnectedEvent ConnectedEventHandler;
        public delegate void LoginEvent();
        public event LoginEvent LoginEventHandler;

        public delegate void P2PConnectEvent(bool isRemote,ConnectionInfo info);
        public event P2PConnectEvent P2PConnectEventHandler;

        public delegate void P2PDataSendSuccessEvent();
        public event P2PDataSendSuccessEvent P2PDataSendSuccessEventHandler;
        public delegate void SendScreenEvent(byte[] data);
        public event SendScreenEvent SendScreenEventHandler;
        public delegate void SendScreenChunksEvent(byte[] data);
        public event SendScreenChunksEvent SendScreenChunksEventHandler;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        private int count = 0;
        public SocketRemoteClient() 
        {
            _isSocketConnected = false;
            _isP2PConnected = false;
            _pingTimer = new System.Threading.Timer(PingServer, null, 0, 10000);

        }
        #region Properties
        public Socket Socket
        {
            get => _socket;
            private set
            {
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
        #region Functions
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
        private void PingServer(object state)
        {
            if(_isSocketConnected)
                SendData(Enums.DataType.PING, new byte[] { (int)Enums.DataType.PING });
        }
        public void Connect(IPEndPoint endPoint)
        {
            try
            {
                if (Socket == null)
                {
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    Socket.NoDelay = true;
                }
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), Socket);
            }
            catch(SocketException ex)
            {
                Console.WriteLine(string.Format("Connect SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Connect Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
            finally
            {
                //Socket.Close();
            }
        }
        public void SendHeader(Enums.DataType type, int dataLength)
        {
            try
            {
                byte[] header = new byte[5];
                header[0] = (byte)type;
                Array.Copy(BitConverter.GetBytes(dataLength), 0, header, 1, 4);
                Socket.BeginSend(header, 0, header.Length, SocketFlags.None, null, null);
            }
            catch
            {
                Console.WriteLine(string.Format("Socket SendHeader error"));

            }
        }
        public void SendData(Enums.DataType type, byte[] data)
        {
            try
            {
                byte[] bytes = new byte[1 + data.Length];
                bytes[0] = (byte)type;
                Array.Copy(data, 0, bytes, 1, data.Length);
                byte[] dataSend = Utils.AddPaddingToBytes(bytes);
                Socket.BeginSend(dataSend, 0, dataSend.Length, SocketFlags.None, null, null);
            }
            catch(SocketException ex)
            {
                Console.WriteLine(string.Format("Socket Send error: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }
        private void ConnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                Socket.EndConnect(asyncResult);
                Console.WriteLine("Client connected: " + Socket.RemoteEndPoint);
                if (Socket.Connected)
                {
                    SocketConnected = true;
                }
                ConnectedEvent connectedEvent = ConnectedEventHandler;
                if(connectedEvent != null)
                {
                    connectedEvent();
                }
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;


                Socket.BeginReceive(stateObject.Buffer, 0, 1024, SocketFlags.None, new AsyncCallback(Callback), stateObject);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(string.Format("ConnectCallback SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch(Exception ex)
            {
                Console.WriteLine(string.Format("ConnectCallback Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }
        private void Callback(IAsyncResult asyncResult)
        {
            try
            {
                StateObject stateObject = (StateObject)asyncResult.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(asyncResult);
                if (num > 0)
                {  

                    stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0 , num);

                    while (!CancellationToken.IsCancellationRequested)
                    {
                        if (!(stateObject.ByteArrayBuilder.Length >= 4))
                        {
                            goto IL_163;
                        }

                        int dataLength = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, 4).ToArray(), 0);
                        int paddingAdded = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(4, 4).ToArray(), 0);

                        if (stateObject.ByteArrayBuilder.Length > MAX_BUFFER_SIZE)
                        {
                            stateObject.ByteArrayBuilder.Clear();
                            goto IL_163;
                        }
                        if(dataLength < 0 || paddingAdded < 0)
                        {
                            stateObject.ByteArrayBuilder.Clear();
                            goto IL_163;
                        }
                        if (dataLength > MAX_BUFFER_SIZE - 8 - paddingAdded)
                        {
                            stateObject.ByteArrayBuilder.Clear();
                            goto IL_163;
                        }
                        if (!(stateObject.ByteArrayBuilder.Length >= dataLength + paddingAdded + 4  + 4))
                        {
                            Console.WriteLine($"Waitting for length {dataLength + 4 + paddingAdded + 4} padding - {paddingAdded}");
                            count++;
                            goto IL_163;
                        }
                        Array src = stateObject.ByteArrayBuilder.Cut(dataLength + 4 + paddingAdded + 4).ToArray();
                        //stateObject.ByteArrayBuilder.Clear();
                        byte[] array = new byte[dataLength];
                        Array.Copy(src, 8, array, 0, dataLength);
                        ProcessDataReceived(array, stateObject);
                        if (CancellationToken.IsCancellationRequested)
                            break;
                    }
                }
            IL_163:
                try
                {
                    workSocket.BeginReceive(stateObject.Buffer, 0, StateObject.BufferSize, SocketFlags.None, Callback, stateObject);
                }
                catch
                {
                    workSocket.Close();
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine(string.Format("Callback SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Callback Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }

        private void ProcessDataReceived(byte[] array, StateObject stateObject)
        {
            byte[] data = array;
            Console.WriteLine($"Data received: {data.Length} bytes");
            int response = data[0];
            switch (response)
            {
                case 0:
                    Console.WriteLine("Ping successfully");
                    break;
                case 1:
                    Console.WriteLine("Login successfully");
                    LoginEvent loginEvent = LoginEventHandler;  
                    if(loginEvent != null)
                    {
                        loginEvent();
                    }
                    _isSocketConnected = true;
                    break;
                case 2:
                    ProcessP2PConnection(data);
                    break;
                case 3:
                    ProcessP2PDataReceived(stateObject, data);
                    break;
                case 4:
                    ProcessP2PCapture(data.Skip(1).ToArray());
                    break;
                case 5:
                    ProcessP2PChunkSend(data.Skip(1).ToArray());
                    break;
                case 20:
                    Console.WriteLine("P2P Data received");
                    break;
                case 30:
                    //p2p data send success
                    P2PDataSendSuccessEvent p2PDataSendSuccess = P2PDataSendSuccessEventHandler;
                    if(p2PDataSendSuccess != null)
                    {
                        p2PDataSendSuccess();
                    }
                    break;
                case 40:
                    //chunk send
                    ProcessP2PCapture(data);
                    break;
                case 90:
                    Console.WriteLine("P2P connect error");
                    break;
                case 97:
                case 98:
                case 99:
                    Console.WriteLine("Error");
                    break;
                default:
                    break;
            }
        }
        private void ProcessP2PChunkSend(byte[] data)
        {
            Console.WriteLine($"Count chunk : {count}");
            SendScreenChunksEvent sendScreenChunks = SendScreenChunksEventHandler;
            if(sendScreenChunks != null)
            {
                sendScreenChunks(data);
            }
        }
        private void ProcessP2PCapture(byte[] data)
        {
            Console.WriteLine($"Count : {count}");
            SendScreenEvent sendScreenEvent = SendScreenEventHandler;
            if (sendScreenEvent != null)
            {
                sendScreenEvent(data);
            }
        }

        private void ProcessP2PDataReceived(StateObject stateObject, byte[] data)
        {
          
        }

        private void ProcessP2PConnection(byte[] data)
        {
            int isRemote = data[1];
            byte[] bytesData = new byte[data.Length - 2];
            Array.Copy(data, 2, bytesData, 0, bytesData.Length);
            string[] dataStrings = Encoding.UTF8.GetString(bytesData).Split('|');
            if(dataStrings.Length != 8)
            {
                throw new Exception("Missing some value");
            }
            ConnectionInfo connectionInfo = new ConnectionInfo(
                sessionId: dataStrings[0],
                partner: new Info
                {
                    Id = dataStrings[1],
                    Password = dataStrings[2],
                    ComputerName = dataStrings[3],
                    Width = int.Parse(dataStrings[4]),
                    Height = int.Parse(dataStrings[5]),
                    MajorVersion = dataStrings[6],
                    MinorVersion = dataStrings[7],
                });
            if (isRemote == 0)
            {
                P2PConnectEvent p2pConnectEvent = P2PConnectEventHandler;
                if (p2pConnectEvent != null)
                {
                    p2pConnectEvent(true, connectionInfo);
                }
            }
            else if(isRemote == 1)
            {
                P2PConnectEvent p2pConnectEvent = P2PConnectEventHandler;
                if (p2pConnectEvent != null)
                {
                    p2pConnectEvent(false,connectionInfo);
                }
            }
            else
            {
                throw new UnauthorizedAccessException("Error when socket connection");
            }
            _isP2PConnected = true;
            Console.WriteLine($"Client Info: {JsonConvert.SerializeObject(connectionInfo, Formatting.Indented)}");
        }
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _socket?.Close();
                _disposed = true;
            }
        }
        #endregion
    }
}
