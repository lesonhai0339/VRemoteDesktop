 using Serilog;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using static VRemoteClient.Models.Enums.KeyboardEnums;

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
        private System.Threading.Timer _timer;
        private ClientInfo _me;
        private ScreenHook _vscreen;
        private GlobalMouseHook _mouseHook;

        public delegate void ConnectSckEvent();
        public delegate void LoginEvent(bool flag);
        public delegate void P2PConnectEvent(bool flag, ConnectionInfo? info);
        public delegate void P2PDataSendSuccessEvent();
        public delegate void P2PScreenEvent(byte[] screen);
        public delegate void P2PChunksEvent(List<ScreenBlock> blocks);
        public delegate void AckEvent();

        public event ConnectSckEvent ConnectSckEventHandler;
        public event LoginEvent LoginEventHandler;
        public event P2PConnectEvent P2PConnectEventHandler;
        public event P2PDataSendSuccessEvent P2PDataSendSuccessEventHandler;
        public event P2PScreenEvent P2PScreenEventHandler;
        public event P2PChunksEvent P2PChunksEventHandler;
        public event AckEvent AckEventHandler;

        CancellationTokenSource _cancellationToken;

        public RemoteClient(ClientInfo me)
        {
            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cancellationToken = new CancellationTokenSource();
            _mouseHook = new GlobalMouseHook();
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            _me = me;
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
        private void PingToServer(object state)
        {
            if (_isSocketConnected)
            {
                if (!_isP2PConnected)
                {
                    Send(CommandType.Ping, new byte[0]);
                }
            }
        }
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


                Socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);

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

                        if(!(stateObject.ByteArrayBuilder.Length >= length))
                        {
                            //Console.WriteLine("Waitting "+ length + " - receive "+ num);
                            break;
                        }
                        Array src = stateObject.ByteArrayBuilder.Cut(length).ToArray();
                        byte[] data = new byte[length - 4];
                        Buffer.BlockCopy(src, 4, data, 0, data.Length);
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
                    ProcessLogin(true);
                    break;
                case CommandType.P2PConnect:
                    ProcessP2PConnect(true, data);
                    break;
                case CommandType.Disconnect:
                    break;
                case CommandType.Data:
                    break;
                case CommandType.Ping:
                    break;
                case CommandType.Pong:
                    Console.WriteLine("Pong received from server");
                    break;
                case CommandType.Screen:
                    ProcessScreen(data);
                    break;
                case CommandType.Chunks:
                    ProcessChunks(data);
                    break;
                case CommandType.Keyboard:
                    ProcessKeyboard(data);
                    break;
                case CommandType.MouseClick:
                    ProcessMouse(data);
                    break;
                case CommandType.MouseMove:
                    ProcessMouse(data);
                    break;
                case CommandType.Error:
                    break;
                case CommandType.LoginFailed:
                    ProcessLogin(false);
                    break;
                case CommandType.PartnerDisconnected:
                    break;
                case CommandType.P2PConnectFailed:
                    ProcessP2PConnect(false, data);
                    break;
                case CommandType.Ack:
                    AckEvent ack = AckEventHandler;
                    if (ack != null)
                    {
                        ack();
                    }
                    break;
                default:
                    break;
            }
        }
        /// <summary>
        /// typ:0 = mouse click, type:1 = mouse move
        /// </summary>
        /// <param name="type"></param>
        /// <param name="data"></param>
        private void ProcessMouse(byte[] data)
        {
            try
            {
                string[] mouseData = Encoding.ASCII.GetString(data, 1, data.Length - 1).Trim().Split('|');
                if (mouseData.Length != 6)
                {
                    Log.ForContext("FileName", "MouseHook").Error("Number of elements not exaclly");
                    return;
                }
                int senderSceenWidth = int.Parse(mouseData[0]);
                int senderScreenHeight = int.Parse(mouseData[1]);
                int receiverScreenWidth = _me.Width;
                int receiverScreenHeight = _me.Height;
                MouseMessage button = (MouseMessage)int.Parse(mouseData[2]);
                MouseType action = (MouseType)int.Parse(mouseData[3]);
                int mouseX = int.Parse(mouseData[4]);
                int mouseY = int.Parse(mouseData[5]);
                bool flag = _mouseHook.MouseEvent(senderSceenWidth, senderScreenHeight, receiverScreenWidth, receiverScreenHeight, button, action, mouseX, mouseY);
                if (!flag)
                {
                    Log.ForContext("FileName", "RemoteClient").Error("Mouse event failed");

                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing mouse data");
            }
        }

        private void ProcessKeyboard(byte[] data)
        {
            try
            {
                string[] keyboards = Encoding.ASCII.GetString(data, 1, data.Length - 1).Trim().Split('|');
                if (keyboards.Length != 4)
                {
                    Log.ForContext("FileName", "KeyboardHook").Error("Number of elements not exaclly");
                }
                IntPtr ptr = (IntPtr)int.Parse(keyboards[0]);
                Keys keyModifier = (Keys)int.Parse(keyboards[1]);
                Keys keyCode = (Keys)int.Parse(keyboards[2]);
                KeyState keyType = (KeyState)int.Parse(keyboards[3]);
                Console.WriteLine($"Keyboard received, Key: {keyCode} - Modifier: {keyModifier} - Type: {keyType}");
                if(keyType == KeyState.KeyDown)
                {
                    if (keyModifier != Keys.None)
                    {
                        KeyboardSimulator.SendKeyCombo(keyModifier, keyCode);
                    }
                    else
                    {
                        KeyboardSimulator.SendKey(keyCode);
                    }
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing keyboard data");
            }
        }
        private void ProcessScreen(byte[] data)
        {
            try
            {
                byte[] screenData = new byte[data.Length - 1];
                Buffer.BlockCopy(data, 1, screenData, 0, data.Length - 1);
                P2PScreenEvent p2pScreen = P2PScreenEventHandler;
                if (p2pScreen != null)
                {
                    p2pScreen(screenData);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing screen data");
            }
        }
        private void ProcessChunks(byte[] data)
        {
            try
            {
                List<ScreenBlock> blocks = new List<ScreenBlock>();
                byte[] chunks = new byte[data.Length - 1];
                Buffer.BlockCopy(data, 1, chunks, 0, data.Length - 1);

                int offset = 0;
                while(offset < chunks.Length)
                {
                    int length = BitConverter.ToInt32(chunks, offset + 0);
                    int x = BitConverter.ToInt32(chunks, offset + 4);
                    int y = BitConverter.ToInt32(chunks, offset + 8);
                    int width = BitConverter.ToInt32(chunks, offset + 12);
                    int height = BitConverter.ToInt32(chunks, offset + 16);
                    byte[] chunk = new byte[length];
                    Buffer.BlockCopy(chunks, offset + 20, chunk, 0, length);

                    offset += length + 20 ;
                    blocks.Add(new ScreenBlock
                    {
                        IsFullScreen = false,
                        Rectangle = new Rectangle(x, y, width, height),
                        Bytes = chunk
                    });
                }
                P2PChunksEvent p2pChunks = P2PChunksEventHandler;
                if(p2pChunks != null)
                {
                    if (blocks.Any())
                    {
                        p2pChunks(blocks);
                    }
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing chunks data");
            }
        }
        private void ProcessLogin(bool flag)
        {
            if (flag)
            {
                LoginEvent loginSuccess = LoginEventHandler;
                if (loginSuccess != null)
                {
                    loginSuccess(true);
                }
            }
            else
            {
                LoginEvent loginSuccess = LoginEventHandler;
                if (loginSuccess != null)
                {
                    loginSuccess(false);
                }
            }
        }
        private void ProcessP2PConnect(bool flag, byte[] data)
        {
            P2PConnectEvent p2pConnect = P2PConnectEventHandler;
            if (p2pConnect == null)
            {
                return;
            }
            if (!flag)
            {
                p2pConnect(false, null);
            }
            else
            {
                try
                {
                    string[] partnerInfo = Encoding.ASCII.GetString(data, 1, data.Length - 1).Split('|');
                    ConnectionInfo connectionInfo = new ConnectionInfo(sessionId: partnerInfo[1]);
                    if (partnerInfo[0].ToLower() == "0")
                    {
                        connectionInfo.Sender = new ClientInfo
                        {
                            Id = partnerInfo[2],
                            Password = partnerInfo[3],
                            ComputerName = partnerInfo[4],
                            Width = int.Parse(partnerInfo[5]),
                            Height = int.Parse(partnerInfo[6]),
                            MajorVersion = partnerInfo[7],
                            MinorVersion = partnerInfo[8],
                        };
                        connectionInfo.Receiver = _me;
                        if(_vscreen == null)
                        {
                            _vscreen = new ScreenHook(this);
                        }
                    }
                    else if(partnerInfo[0].ToLower() == "1")
                    {
                        connectionInfo.Receiver = new ClientInfo
                        {
                            Id = partnerInfo[2],
                            Password = partnerInfo[3],
                            ComputerName = partnerInfo[4],
                            Width = int.Parse(partnerInfo[5]),
                            Height = int.Parse(partnerInfo[6]),
                            MajorVersion = partnerInfo[7],
                            MinorVersion = partnerInfo[8],
                        };
                        connectionInfo.Sender = _me;
                    }
                    else
                    {
                        Log.ForContext("FileName", "RemoteClient").Error("Invalid P2P connection data format: {Data}", Encoding.ASCII.GetString(data, 1, data.Length - 1));
                        p2pConnect(false, null);
                        return;
                    }
                    p2pConnect(true, connectionInfo);
                    _isP2PConnected = true;
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing P2P connection data");
                    p2pConnect(false, null);
                }
            }
        }
        /// <summary>
        /// send data with spicific length
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="data"></param>
        /// <param name="sendLength"></param>
        public void Send(CommandType commandType, byte[] data, int sendLength)
        {
            try
            {
                Socket.BeginSend(data, 0, sendLength, SocketFlags.None, null, null);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server with specific length");
            }
        }
        /// <summary>
        /// send data with header(option)
        /// </summary>
        /// <param name="commandType"></param>
        /// <param name="data"></param>
        /// <param name="sendHeader"></param>
        public void Send(CommandType commandType, byte[] data, bool sendHeader = true)
        {
            try
            {
                if (sendHeader)
                {
                    //send data with header
                    byte[] dataWithHeader = new byte[data.Length + 5];
                    Buffer.BlockCopy(BitConverter.GetBytes(dataWithHeader.Length), 0, dataWithHeader, 0, 4);
                    dataWithHeader[4] = (byte)commandType; //set command type
                    Buffer.BlockCopy(data, 0, dataWithHeader, 5, data.Length);
                    Socket.BeginSend(dataWithHeader, 0, dataWithHeader.Length, SocketFlags.None, null, null);
                }
                else
                {
                    //send data
                    Socket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
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
