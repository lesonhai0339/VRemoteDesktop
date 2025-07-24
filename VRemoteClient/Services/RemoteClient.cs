using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;
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

        private object _lockObject = new object();

        private Socket _socket;
        private Thread _screenThread;
        //private System.Threading.Timer _timer;
        private ClientInfo _me;
        private ScreenHook _vscreen;

        private ConcurrentQueue<object> _screenTasks;
        private ConcurrentQueue<object> _commandTasks;
        private BackgroundWorker _backgroundWorker;

        public delegate void ConnectSckEvent();
        public delegate void LoginEvent(bool flag);
        public delegate void P2PConnectEvent(bool flag, ConnectionInfo? info);
        public delegate void P2PDataSendSuccessEvent();
        public delegate void P2PScreenEvent(byte[] screen);
        public delegate void P2PChunksEvent(List<ScreenBlock> blocks);
        public delegate void AckEvent();
        public delegate void ScreenSuccessEvent(bool flag);
        public delegate void ChunksSuccessEvent(bool flag);
        public delegate void P2PDisconnectedEvent();

        public event ConnectSckEvent ConnectSckEventHandler;
        public event LoginEvent LoginEventHandler;
        public event P2PConnectEvent P2PConnectEventHandler;
        public event P2PDataSendSuccessEvent P2PDataSendSuccessEventHandler;
        public event P2PScreenEvent P2PScreenEventHandler;
        public event P2PChunksEvent P2PChunksEventHandler;
        public event AckEvent AckEventHandler;
        public event ScreenSuccessEvent ScreenSuccessEventHandler;
        public event ChunksSuccessEvent ChunksSuccessEventHandler;
        public event P2PDisconnectedEvent P2PDisconnectedEventhandler;

        CancellationTokenSource _cancellationToken;

        public RemoteClient(ClientInfo me)
        {
            _isSocketConnected = false;
            _isP2PConnected = false;
            _isDisposed = false;
            _cancellationToken = new CancellationTokenSource();
            //_timer = new Timer(PingToServer, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5));
            _me = me;
            ScreenTasks = new ConcurrentQueue<object>();
            CommandTasks = new ConcurrentQueue<object>();
            Worker = new BackgroundWorker();
            Worker.WorkerSupportsCancellation = true;


            _screenThread = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                _vscreen = new ScreenHook(this);
            });
            _screenThread.Start();
        }
        #region Properties
        public bool IsP2PConnected
        {
            get
            {
                lock (_lockObject)
                {
                   return _isP2PConnected;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _isP2PConnected = value;
                }
            }
        }
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
        public BackgroundWorker Worker
        {
            get => _backgroundWorker;
            set
            {
                if (_backgroundWorker != null)
                {
                    _backgroundWorker.DoWork -= DoWork;
                }

                _backgroundWorker = value;

                if (_backgroundWorker != null)
                {
                    _backgroundWorker.DoWork += DoWork;
                }
            }
        }
        public ConcurrentQueue<object> ScreenTasks
        {
            get => _screenTasks;
            private set
            {
                _screenTasks = value;
            }
        }
        public ConcurrentQueue<object> CommandTasks
        {
            get => _commandTasks;
            private set
            {
                _commandTasks = value;
            }
        }
        #endregion
        #region Methods
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var taskQueue = DequeueTask();
                if (taskQueue != null)
                {
                    try
                    {
                        if (taskQueue is TaskObject task)
                        {
                            ProcessSingleTask(task);
                        }
                        else if (taskQueue is TaskGroup taskGroup)
                        {
                            foreach (var t in taskGroup.Tasks)
                            {
                                if (CommandTasks.TryPeek(out _))
                                {
                                    break;
                                }
                                ProcessSingleTask(t);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                }
                Thread.Sleep(1);
            }
        }
        private void ProcessSingleTask(TaskObject task)
        {
            switch (task.TaskType)
            {
                case CommandType.None:
                    Send(commandType: task.TaskType, data: task.Data, sendLength: task.Length);
                    break;
                default:
                    Send(commandType: task.TaskType, data: task.Data, sendHeader: task.IsSendHeader);
                    break;
            }
        }
        private object? DequeueTask()
        {
            if (CommandTasks.TryDequeue(out var cmdTask))
            {
                return cmdTask;
            }
            else
            {
                return ScreenTasks.TryDequeue(out var tasks) ? tasks : null;
            }
        }
        public void AddWork(TaskObject task, QueueTask type = QueueTask.Command)
        {
            if (type == QueueTask.Screen)
            {
                if (ScreenTasks.Count >= 2)
                {
                    // keep last frame and remove all previous frames
                    var temp = new List<object>();
                    while (ScreenTasks.TryDequeue(out var item) && temp.Count == 0)
                    {
                        temp.Add(item);
                    }
                    foreach (var item in temp.Take(1))
                    {
                        ScreenTasks.Enqueue(item);
                    }
                }
                ScreenTasks.Enqueue(task);
            }
            else
            {
                CommandTasks.Enqueue(task);
            }
        }
        public void AddWorkGroup(List<TaskObject> tasks, QueueTask type = QueueTask.Command)
        {
            if (type == QueueTask.Screen)
            {
                ScreenTasks.Enqueue(new TaskGroup(tasks));
            }
            else
            {
                CommandTasks.Enqueue(new TaskGroup(tasks));
            }
        }
        //private void Ping(object state)
        //{
        //    if (_isSocketConnected)
        //    {
        //        if (!_isP2PConnected)
        //        {
        //            AddWork(new TaskObject
        //            (
        //                taskType : CommandType.Ping,
        //                data: new byte[0]
        //            ));
        //        }
        //    }
        //}
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }
        /// <summary>
        /// Connect to remote server with default IP and port
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public void Connect(string ip = REMOTE_SERVER_IP, int port = REMOTE_SERVER_PORT)
        {
            try
            {
                if (!Worker.IsBusy)
                {
                    Worker.RunWorkerAsync();
                }
                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out IPAddress _))
                {
                    remoteEP = new IPEndPoint(IPAddress.Parse(ip), port);

                    if (Socket == null)
                    {
                        Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        Socket.NoDelay = true;
                    }
                    Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    Socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), Socket);
                }
                else
                {
                    Log.ForContext("FileName", "RemoteClient").Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when connect to relay server");
            }
            finally
            {

            }
        }
        /// <summary>
        /// Callback method when the socket is connected to the remote server
        /// </summary>
        /// <param name="ar"></param>
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
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when connecting to remote server");
            }
        }
        /// <summary>
        /// callback method when data is received from the remote server
        /// </summary>
        /// <param name="ar"></param>
        private void DataCallback(IAsyncResult ar)
        {
            try
            {
                StateObject stateObject = (StateObject)ar.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(ar);
                if (num > 0)
                {
                    stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0, num);
                    while (!_cancellationToken.Token.IsCancellationRequested)
                    {
                        if (!(stateObject.ByteArrayBuilder.Length >= 4))
                        {
                            break;
                        }
                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, 4).ToArray(), 0);

                        if (!(stateObject.ByteArrayBuilder.Length >= length))
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
                catch (SocketException ex)
                {
                    Log.ForContext("FileName", "RemoteClient").Error(ex, "Begin receive error");
                    //Socket.Close();
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Unexpected error when receiving data from remote server");
            }
        }
        private void ProcessReceiveData(byte[] data)
        {
            try
            {
                CommandType commandType = (CommandType)data[0];
                switch (commandType)
                {
                    case CommandType.Login:
                        ProcessLogin(true);
                        break;
                    case CommandType.P2PConnect:
                        Console.WriteLine("A");
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
                    case CommandType.ScreenOk:
                        ScreenSuccessEvent screenSuccess = ScreenSuccessEventHandler;
                        if (screenSuccess != null)
                        {
                            screenSuccess(true);
                        }
                        break;
                    case CommandType.ChunksOk:
                        ChunksSuccessEvent chunksSuccess = ChunksSuccessEventHandler;
                        if (chunksSuccess != null)
                        {
                            chunksSuccess(true);
                        }
                        break;
                    case CommandType.Keyboard:
                        ProcessKeyboard(data);
                        break;
                    case CommandType.Mouse:
                        ProcessMouse(data);
                        break;
                    case CommandType.Error:
                        break;
                    case CommandType.LoginFailed:
                        ProcessLogin(false);
                        break;
                    case CommandType.P2PDisconnect:
                        Console.WriteLine("P2PDisconnect Called");
                        IsP2PConnected = false;
                        P2PDisconnectedEvent disConnected = P2PDisconnectedEventhandler;
                        if (disConnected != null)
                        {
                            disConnected();
                        }
                        _vscreen.StopCapture();
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
            catch(Exception ex)
            {
                Log.ForContext("FileName", "MouseHook").Error(ex, "ProcessReceiveData error");
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

                bool flag = VirtualMouse.MouseEvent(senderSceenWidth, senderScreenHeight, receiverScreenWidth, receiverScreenHeight, button, action, mouseX, mouseY);
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

                VirtualKeyboard.ProcessKeyboardReceived(keyCode, keyType);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing keyboard data");
            }
        }
        private void ProcessScreen(byte[] data)
        {
            try
            {
                string stringHashReceived = Encoding.ASCII.GetString(data, 1, 40);

                var compressedLength = data.Length - 41; // 1 byte header + 40 hash
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 41, compressedData, 0, compressedLength);

                string screenHash = Utils.Extensions.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] screenDecompressed = Utils.Extensions.DecompressGzip(compressedData);
                    P2PScreenEvent p2pScreen = P2PScreenEventHandler;
                    if (p2pScreen != null)
                    {
                        p2pScreen(screenDecompressed);
                    }
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
                Log.ForContext("FileName", "Chunks").Info("Received: {"+ data.Length + "} bytes at: "+ DateTime.Now.ToString("hh:mm:ss.fff"));
                string stringHashReceived = Encoding.ASCII.GetString(data, 1, 40);

                var compressedLength = data.Length - 41; // 1 byte header + 40 hash
                var compressedData = new byte[compressedLength];
                Buffer.BlockCopy(data, 41, compressedData, 0, compressedLength);

                string screenHash = Utils.Extensions.SHAHash(compressedData);

                if (string.Compare(stringHashReceived, screenHash) == 0)
                {
                    byte[] chunksDecompressed = Utils.Extensions.DecompressGzip(compressedData);

                    List<ScreenBlock> blocks = new List<ScreenBlock>();
                    int offset = 0;
                    while (offset < chunksDecompressed.Length)
                    {
                        if (offset + 20 > chunksDecompressed.Length)
                            break;

                        int length = BitConverter.ToInt32(chunksDecompressed, offset + 0);
                        int x = BitConverter.ToInt32(chunksDecompressed, offset + 4);
                        int y = BitConverter.ToInt32(chunksDecompressed, offset + 8);
                        int width = BitConverter.ToInt32(chunksDecompressed, offset + 12);
                        int height = BitConverter.ToInt32(chunksDecompressed, offset + 16);

                        if (offset + 20 + length > chunksDecompressed.Length)
                            break;

                        byte[] chunk = new byte[length];
                        Buffer.BlockCopy(chunksDecompressed, offset + 20, chunk, 0, length);

                        offset += length + 20;
                        blocks.Add(new ScreenBlock
                        {
                            IsFullScreen = false,
                            Rectangle = new Rectangle(x, y, width, height),
                            Bytes = chunk
                        });
                    }
                    P2PChunksEvent p2pChunks = P2PChunksEventHandler;
                    if (p2pChunks != null)
                    {
                        if (blocks.Count > 0)
                        {
                            p2pChunks(blocks);
                        }
                    }
                }
            }
            catch (Exception ex)
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
                        _vscreen?.StartCapture();   
                    }
                    else if (partnerInfo[0].ToLower() == "1")
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
                    IsP2PConnected = true;
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
                Socket.BeginSend(data, 0, sendLength, SocketFlags.None, (ar) =>
                {
                    try
                    {
                        Socket.EndSend(ar);
                    }
                    catch (SocketException ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                    }
                }, null);
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
                    Socket.BeginSend(dataWithHeader, 0, dataWithHeader.Length, SocketFlags.None, (ar) =>
                    {
                        try
                        {
                            Socket.EndSend(ar);
                        }
                        catch (SocketException ex)
                        {
                            Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                        }
                    }, null);
                }
                else
                {
                    //send data
                    Socket.BeginSend(data, 0, data.Length, SocketFlags.None, (ar) =>
                    {
                        try
                        {
                            Socket.EndSend(ar);
                        }
                        catch (SocketException ex)
                        {
                            Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("FileName", "RemoteClient").Error(ex, "Send error");
                        }
                    }, null);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error when sending data to remote server without specific length");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    //background worker
                    if (Worker.IsBusy)
                    {
                        Worker.CancelAsync();
                        int timeout = 5000;
                        while (Worker.IsBusy && timeout > 0)
                        {
                            Thread.Sleep(100);
                            timeout -= 100;
                        }
                    }
                    Worker.DoWork -= DoWork;
                    _backgroundWorker.Dispose();
                    _backgroundWorker = null;


                    //queue
                    if (_screenTasks != null)
                    {
                        while (_screenTasks.TryDequeue(out var item))
                        {
                            if (item is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
                        }
                        _screenTasks = null;
                    }
                    if (_commandTasks != null)
                    {
                        while (_commandTasks.TryDequeue(out var item))
                        {
                            if (item is IDisposable disposableItem)
                            {
                                disposableItem.Dispose();
                            }
                        }
                        _commandTasks = null;
                    }

                    if (_screenThread != null && _screenThread.IsAlive)
                    {
                        _screenThread.Join(2000);
                    }
                    _screenThread = null;
                    // Screen hook  
                    _vscreen?.StopCapture();

                    if (_vscreen?.IsCapturing == true)
                    {
                        int timeout = 2000;
                        while (_vscreen.IsCapturing && timeout > 0)
                        {
                            Thread.Sleep(100);
                            timeout -= 100;
                        }
                    }
                    _vscreen?.Dispose();
                    _vscreen = null;


                    if (_cancellationToken != null)
                    {
                        try
                        {
                            _cancellationToken.Cancel();
                            _cancellationToken.Dispose();
                            _cancellationToken = null;
                        }
                        catch (ObjectDisposedException)
                        {
                        }
                    }

                    try
                    {
                        _socket?.Shutdown(SocketShutdown.Both);
                        _socket?.Close();
                        _socket?.Dispose();
                        _socket = null;
                    }
                    catch (Exception)
                    {
                    }

                    ConnectSckEventHandler = null;
                    LoginEventHandler = null;
                    P2PConnectEventHandler = null;
                    P2PDataSendSuccessEventHandler = null;
                    P2PScreenEventHandler = null;
                    P2PChunksEventHandler = null;
                    AckEventHandler = null;
                    ScreenSuccessEventHandler = null;
                    ChunksSuccessEventHandler = null;

                    // Clear other objects
                    _me = null;
                    _lockObject = null;

                    // Set flags
                    _isSocketConnected = false;
                    _isP2PConnected = false;
                    _isDisposed = true;
                }
            }
        }
        #endregion
    }
}
