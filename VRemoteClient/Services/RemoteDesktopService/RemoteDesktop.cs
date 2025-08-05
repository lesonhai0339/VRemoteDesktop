using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Services.ScreenService;
using VRemoteClient.Utils;
using VRemoteClient.Services.KeyboardService;
using VRemoteClient.Services.ConnectionService;
using System.Configuration;
using System.Collections.Concurrent;
using System.ComponentModel;
using VRemoteClient.Services.MouseService;
using VRemoteClient.Services.RemoteClientService;
using static VRemoteClient.Models.Enums.KeyState;

namespace VRemoteClient.Services.RemoteDesktopService
{
    public class RemoteDesktop: IDisposable
    {
        private readonly string SSID = "0000000000000000";
        private readonly object _lockProperties = new object();
        private bool _isDisposed = false;
        private volatile bool _isSocketConnectSuccess;

        private Thread _screenThread;
        private ManualResetEvent _resetEvent;

        private ClientInfo _ownerInfo;

        private GlobalKeyboardHook _globakKeyboardHook;
        private IGlobalScreenCapture _globakScreenHook;
        private RemoteClient _remoteClient;
        private ConnectionManager _connectionManager;


        private ConcurrentQueue<object> _screenTasks;
        private ConcurrentQueue<object> _commandTasks;
        private BackgroundWorker _backgroundWorker;
        private CancellationTokenSource _cancellationToken;


        public event Action<bool> ConnectServerEvent;
        public event Action<bool> LoginEvent;
        public event Action<bool, ConnectionInfo> P2PConnectEvent;
        public event Action<object, CustomKeyMessageEventArgs> KeyboardEvent;
        public event Action<byte[]> ScreenEvent;
        public event Action<byte[]> ChunksEvent;
        public event Action<bool> P2PDisconnect;
        public RemoteDesktop()
        {
            OwnerInfo = Extensions.InitInfo();

            ScreenTasks = new ConcurrentQueue<object>();
            CommandTasks = new ConcurrentQueue<object>();

            _resetEvent = new ManualResetEvent(false);
            _cancellationToken = new CancellationTokenSource();

            Worker = new BackgroundWorker();
            Worker.WorkerSupportsCancellation = true;

            InitializeCompoment();
        }
        public void InitializeCompoment()
        {
            KeyboardHook ??= new GlobalKeyboardHook();
            RemoteClient ??= new RemoteClient(OwnerInfo);
            _connectionManager ??= new ConnectionManager();
            Task.Factory.StartNew(() =>
            {
                ScreenHook = new GlobalScreenCapture(null, null);

            },TaskCreationOptions.LongRunning);
            if (!Worker.IsBusy)
            {
                Worker.RunWorkerAsync();
            }
        }
        #region Properties
        public bool IsSocketConnected
        {
            get => _isSocketConnectSuccess;
            private set => _isSocketConnectSuccess = value;
        }
        public ClientInfo OwnerInfo
        {
            get => _ownerInfo;
            set
            {
                _ownerInfo = value;
            }
        }
        public GlobalKeyboardHook KeyboardHook
        {
            get
            {
                lock (_lockProperties)
                {
                    return _globakKeyboardHook;
                }
            }
            set
            {
                lock (_lockProperties)
                {
                    if(_globakKeyboardHook != null)
                    {
                        _globakKeyboardHook.KeyPressed -= KeyboardPressedEvent;
                    }
                    _globakKeyboardHook = value;
                    if(_globakKeyboardHook != null)
                    {
                        _globakKeyboardHook.KeyPressed += KeyboardPressedEvent;
                    }
                }
            }
        }
        public IGlobalScreenCapture ScreenHook
        {
            get
            {
                lock (_lockProperties)
                {
                    return _globakScreenHook;
                }
            }
            set
            {
                lock (_lockProperties)
                {
                    if (_globakScreenHook != null)
                    {
                        _globakScreenHook.ScreenEvent -= ScreenHookEventHandler;
                    }
                    _globakScreenHook = value;
                    if (_globakScreenHook != null)
                    {
                        _globakScreenHook.ScreenEvent += ScreenHookEventHandler;
                    }
                }
            }
        }
        public RemoteClient RemoteClient
        {
            get
            {
                lock (_lockProperties)
                {
                    return _remoteClient;
                }
            }
            set
            {
                lock (_lockProperties)
                {
                    if (_remoteClient != null)
                    {
                        _remoteClient.ConnectEvent -= ConnectEventHandler;
                        _remoteClient.LoginEvent -= LoginEventHandler;
                        _remoteClient.P2PConnectEvent -= P2PConnectEventHandler;
                        _remoteClient.ScreenEvent -= P2PScreenEventHandler;
                        _remoteClient.ChunksEvent -= P2PChunksEventHandler;
                        _remoteClient.ScreenSuccessEvent -= ScreenSuccessEventHandler;
                        _remoteClient.ChunksSuccessEvent -= ChunksSuccessEventHandler;
                        _remoteClient.MouseReceivedEvent -= MouseReceivedEventHandler;
                        _remoteClient.KeyboardReceivedEvent -= KeyboardReceivedEventHandler;
                        _remoteClient.P2PDisconnectedEvent -= P2PDisconnectedEventhandler;
                        _remoteClient.ClipboardReceivedEvent -= ClipboardReceivedEventHandler;
                    }
                    _remoteClient = value;
                    if (_remoteClient != null)
                    {
                        _remoteClient.ConnectEvent += ConnectEventHandler;
                        _remoteClient.LoginEvent += LoginEventHandler;
                        _remoteClient.P2PConnectEvent += P2PConnectEventHandler;
                        _remoteClient.ScreenEvent += P2PScreenEventHandler;
                        _remoteClient.ChunksEvent += P2PChunksEventHandler;
                        _remoteClient.ScreenSuccessEvent += ScreenSuccessEventHandler;
                        _remoteClient.ChunksSuccessEvent += ChunksSuccessEventHandler;
                        _remoteClient.MouseReceivedEvent += MouseReceivedEventHandler;
                        _remoteClient.KeyboardReceivedEvent += KeyboardReceivedEventHandler;
                        _remoteClient.P2PDisconnectedEvent += P2PDisconnectedEventhandler;
                        _remoteClient.ClipboardReceivedEvent += ClipboardReceivedEventHandler;

                    }
                }
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
        public void Cancel()
        {
            _cancellationToken.Cancel();
        }
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            int count = 0;
            while (!_cancellationToken.IsCancellationRequested)
            {
                if(count % 10 == 0)
                {
                    if (ScreenHook != null)
                    {
                        if (ScreenHook.IsCapturing && _connectionManager.NumberOfConnections == 0)
                        {
                           StopScreenHook();
                        }
                    }  
                    count = 0;
                }

                var taskQueue = DequeueTask();
                if (taskQueue != null)
                {
                    try
                    {
                        if (taskQueue is TaskObject task)
                        {
                            ProcessTask(task);
                        }
                        else if (taskQueue is TaskGroup taskGroup)
                        {
                            foreach (var t in taskGroup.Tasks)
                            {
                                if (CommandTasks.TryPeek(out _))
                                {
                                    break;
                                }
                                ProcessTask(t);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", "RemoteClient").Error(ex, "Dowork error");
                    }
                }
                count++;
                Thread.Sleep(5);
            }
        }
        private void ProcessTask(TaskObject task)
        {
            byte[] data = TaskObjectToBytes(task);
            RemoteClient.Send(data);
        }
        private object? DequeueTask()
        {
            try
            {
                if (CommandTasks.Count > 0)
                {
                    return CommandTasks.TryDequeue(out var tasks) ? tasks : null;
                }
                else
                {
                    return ScreenTasks.TryDequeue(out var tasks) ? tasks : null;
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "DequeueTask error");
                return null;
            }
        }
        public void AddWork(TaskObject task, DataType type = DataType.Command)
        {
            if (type == DataType.Screen)
            {
                if (ScreenTasks.Count >= 2)
                {
                    // keep last frame and remove all previous frames
                    object? lastItem = null;
                    while (ScreenTasks.TryDequeue(out var item))
                    {
                        lastItem = item;
                    }
                    if (lastItem != null)
                    {
                        ScreenTasks.Enqueue(lastItem);
                    }
                }
                ScreenTasks.Enqueue(task);
            }
            else
            {
                CommandTasks.Enqueue(task);
            }
        }
        public void AddWorkGroup(List<TaskObject> tasks, DataType type = DataType.Command)
        {
            if (type == DataType.Screen)
            {
                ScreenTasks.Enqueue(new TaskGroup(tasks));
            }
            else
            {
                CommandTasks.Enqueue(new TaskGroup(tasks));
            }
        }
        /// <summary>
        /// Start listening keyboard event(low-level keyboard hook). Listen on current process by process Id
        /// </summary>
        public void StartKeyboardHook()
        {
            try
            {
                KeyboardHook.Start((uint)Process.GetCurrentProcess().Id);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Start keyboard hook failed");
            }
        }
        /// <summary>
        ///  Stop listening keyboard event(low-level keyboard hook)
        /// </summary>
        public void StopKeyboardHook()
        {
            try
            {
                KeyboardHook.Stop();
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Stop keyboard hook failed");
            }
        }
        /// <summary>
        /// Start listen keyboard event on specific Form by Form windows handle
        /// </summary>
        /// <param name="handle"></param>
        public void AddKeyboardHookByHandle(IntPtr handle)
        {
            try
            {
                KeyboardHook.AddHook(handle);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Add keyboard hook failed");
            }
        }
        /// <summary>
        /// Stop listen keyboard event on specific Form by Form windows handle
        /// </summary>
        /// <param name="handle"></param>
        public void RemoveKeyboardHookByHandle(IntPtr handle)
        {
            try
            {
                KeyboardHook.RemoveHook(handle);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Remove keyboard hook failed");
            }
        }
        /// <summary>
        /// Start capturing the Windows screen on this window. This method will capture the screen and push data to the
        /// <see cref="ScreenHookEventHandler"/> event, with <see cref="CustomScreenEventArgs"/> as the event args.
        /// </summary>
        public void StartScreenHook()
        {
            try
            {
                ScreenHook.StartCapture();
                ScreenHook.IsCapturing = true;
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Start screen hook failed");
            }
        }
        /// <summary>
        /// Stop capture windows screen on this windows(not completely closed). Can restart capture by call 
        /// <see cref="StartScreenHook"/> method
        /// </summary>
        public void StopScreenHook()
        {
            try
            {
                ScreenHook.StopCapture();
                ScreenHook.IsCapturing = false;
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Stop screen hook failed");
            }
        }
        public void ConnectToServer()
        {
            _resetEvent.Reset();
            try
            {
                string serverIp = ConfigurationManager.AppSettings["RemoteServerIP"];
                string serverPort = ConfigurationManager.AppSettings["RemoteServerPort"];

                if(string.IsNullOrEmpty(serverIp) || !int.TryParse(serverPort, out var port))
                {
                    Log.ForContext("Filename", GetType().Name).Error("Error when connect to server");
                    ConnectServerEvent?.Invoke(false);
                    return;
                }

                RemoteClient.Connect(serverIp, port);
                bool flag = _resetEvent.WaitOne(5000);
                if (!flag)
                {
                    ConnectServerEvent?.Invoke(flag);
                    //TODO: invoke event form main from notify that login failed
                    Log.ForContext("Filename", GetType().Name).Error("Socket connect failed");
                    return;
                }
                Login();
                StartKeyboardHook();
            }
            catch(Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error("ConnectToServer error");
            }
        }
        public void InitP2PConnection(string partnerId, string partnerPassword)
        {
            try
            {
                string id = partnerId.Replace(" ", "");
                string password = partnerPassword.Replace(" ", "");

                string dataString = Extensions.DataStringBuilder(
                    new string[] { 
                        OwnerInfo.Id,
                        id,
                        password
                    }
                );
                byte[] data = Encoding.ASCII.GetBytes(dataString);

                AddWork(new TaskObject
                {
                    TaskType = ResponseType.P2PConnect,
                    Data = data
                });
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "P2P connection error");
            }
        }
        public string FormatKeyboardInput(IntPtr command, Keys modifier, Keys code, KeyState type)
        {
            return KeyboardHook.KeyboardEventTostring(command, modifier, code, type);
        }
        public string GetClipboard()
        {
            try
            {
                return VirtualClipboard.GetClipboardString();
            }
            catch(Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "GetClipboard error");
                return string.Empty;
            }
        }
        /// <summary>
        /// Default using CF_UNICODETEXT format then need to convert string data to UTF-16
        /// (like this: <c>byte[] formatted = Encoding.Unicode.GetBytes(<paramref name="data"/> + '\0');</c>)
        /// </summary>
        /// <param name="data">The input string that will be encoded as UTF-16.</param>
        /// <returns>Formatted byte array.</returns>
        public bool SetClipboard(byte[] data)
        {
            try
            {
                return VirtualClipboard.SetClipboard(data, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
            }
            catch (Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "SetClipboard error");
                return false;
            }
        }
        private void Login()
        {
            try
            {
                string data = Extensions.DataStringBuilder(new string[] { OwnerInfo.ToNetworkPacketString() });
                byte[] dataBytes = Encoding.ASCII.GetBytes(data);
                AddWork(new TaskObject
                {
                    TaskType = ResponseType.Login,
                    Data = dataBytes,
                    IsSendHeader = true
                });
            }
            catch(Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "Login error");
            }
        }
        private byte[] TaskObjectToBytes(TaskObject task)
        {
            if (task.IsSendHeader)
            {
                byte[] resultBytes = new byte[task.Data.Length + 21];

                //sessionId
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(task.SessionId), 0, resultBytes, 0, 16);

                //length
                Buffer.BlockCopy(BitConverter.GetBytes(resultBytes.Length), 0, resultBytes, 16, 4);

                //type
                resultBytes[20] = (byte)task.TaskType; //set command type
                Buffer.BlockCopy(task.Data, 0, resultBytes, 21, task.Length);

                return resultBytes;
            }
            else
            {
                return task.Data;
            }
        }
        #endregion
        #region Events
        private void ConnectEventHandler()
        {
            IsSocketConnected = true;
            _resetEvent.Set();
        }
        /// <summary>
        /// Callback when login successed
        /// </summary>
        /// <param name="flag"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void LoginEventHandler(bool flag)
        {
            LoginEvent?.Invoke(flag);
        }
        /// <summary>
        /// Partner connect callback
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="info"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void P2PConnectEventHandler(bool flag, byte[] data)
        {
            if(!flag)
            {
                P2PConnectEvent?.Invoke(false, null);
                return;
            }               
            try
            {
                ConnectionInfo connectionInfo = _connectionManager.ConvertFromBytes(data, 1, data.Length - 1);
                if (connectionInfo != null)
                {
                    _connectionManager.AddConnection(connectionInfo.SessionId, connectionInfo);
                    if (connectionInfo.Sender != null)
                    {
                        connectionInfo.Receiver = OwnerInfo;
                        if (!ScreenHook.IsCapturing)
                        {
                            StartScreenHook();
                        }
                    }
                    else
                    {
                        connectionInfo.Sender = OwnerInfo;
                        P2PConnectEvent?.Invoke(true, connectionInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing P2P connection data");
            }
        }
        private void P2PScreenEventHandler(byte[] data)
        {
            byte[] screen = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, screen, 0, data.Length - 1);
            ScreenEvent?.Invoke(screen);
        }
        private void P2PChunksEventHandler(byte[] data)
        {
            byte[] chunks = new byte[data.Length - 1];
            Buffer.BlockCopy(data, 1, chunks, 0, data.Length - 1);
            ChunksEvent?.Invoke(chunks);
        }
        private void MouseReceivedEventHandler(byte[] obj)
        {
            try
            {
                byte[] mouse = new byte[obj.Length - 1];
                Buffer.BlockCopy(obj, 1, mouse, 0, obj.Length - 1);

                var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(mouse, OwnerInfo.Width, OwnerInfo.Height);

                bool flag = VirtualMouse.MouseEvent(mouseEvent);
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
        private void KeyboardReceivedEventHandler(byte[] obj)
        {
            try
            {
                int length = obj.Length - 1;
                byte[] keyboard = new byte[length];
                Buffer.BlockCopy(obj, 1, keyboard, 0, length);

                var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(keyboard);
                VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);

            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing keyboard data");
            }
        }
        private void P2PDisconnectedEventhandler(bool flag, string sessionId)
        {
            try
            {
                if (_connectionManager.NumberOfConnections > 0)
                {
                    bool f = _connectionManager.RemoveConnection(sessionId);
                    if (!f)
                        Log.ForContext("Filename", GetType().Name).Error("Cannot remove connection with sessionId " + sessionId);
                }
                P2PDisconnect?.Invoke(flag);

            }
            finally
            {
                if(_connectionManager.NumberOfConnections == 0)
                {
                    StopScreenHook();
                }
            }
        }
        private void KeyboardPressedEvent(object sender, CustomKeyMessageEventArgs e)
        {
            //'Receive' will send clipboard data to all connections when copy pressed globally (not from app forms)
            if (e.Combination == KeyCombination.Copy && e.Handle == IntPtr.Zero && e.IsSynthetic)
            {
                string clipboard = GetClipboard();

                if (string.IsNullOrEmpty(clipboard)) return;

                foreach(var connection in _connectionManager.GetCurrentConnections())
                {
                    AddWork(new TaskObject
                    {
                        TaskType = ResponseType.Clipboard,
                        SessionId = connection.SessionId,
                        Data = Encoding.UTF8.GetBytes(clipboard),
                    });
                }
            }

            //'Sender' will send clipboard to receiver
            KeyboardEvent?.Invoke(sender, e);
        }
        private void ScreenHookEventHandler(object sender, CustomScreenEventArgs e)
        {
            try
            {
                if (e.Data.Count == 0 || e.TotalSize == 0)
                {
                    Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return;
                }

                //byte[] screenHeader = new byte[5];

                //Buffer.BlockCopy(BitConverter.GetBytes(e.TotalSize + 5), 0, screenHeader, 0, 4);
                //screenHeader[4] = (byte)e.Type;

                //header, 16 byte for sessionID(this using defaultSessionId: "0000000000000000"), 4 bytes for data length, 1 byte for type
                byte[] screenHeader = new byte[21];
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(SSID), 0, screenHeader, 0, 16);
                Buffer.BlockCopy(BitConverter.GetBytes(e.TotalSize + 21), 0, screenHeader, 16, 4);
                screenHeader[20] = (byte)e.Type;

                List<TaskObject> tasks = new List<TaskObject>();
                tasks.Add(new TaskObject
                {
                    TaskType = e.Type,
                    Data = screenHeader,
                    IsSendHeader = false
                });
                   
                //data
                for (int i = 0; i < e.Data.Count; i++)
                {
                    var task = new TaskObject
                    {
                        TaskType = e.Type,
                        Data = e.Data[i],
                        IsSendHeader = false
                    };

                    tasks.Add(task);
                }
                AddWorkGroup(tasks);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void ClipboardReceivedEventHandler(byte[] clipboardData)
        {
            var data = VirtualClipboard.DecodeClipboard(clipboardData, 1, clipboardData.Length - 1);
            SetClipboard(data);
        }
        private void ChunksSuccessEventHandler(bool flag)
        {
        }
        private void ScreenSuccessEventHandler(bool flag)
        {
            
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
                    try
                    {
                        Cancel();
                        if (_commandTasks != null)
                        {
                            while (_commandTasks.TryDequeue(out var item))
                            {
                                if (item is IDisposable disposableItem)
                                {
                                    disposableItem.Dispose();
                                }
                            }
                        }
                        if (_screenTasks != null)
                        {
                            while (_screenTasks.TryDequeue(out var item))
                            {
                                if (item is IDisposable disposableItem)
                                {
                                    disposableItem.Dispose();
                                }
                            }
                        }


                        //TODO: dispose here
                        if (_globakKeyboardHook != null)
                        {
                            _globakKeyboardHook.Stop();
                            _globakKeyboardHook.KeyPressed -= KeyboardPressedEvent;
                            _globakKeyboardHook.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.ForContext("FileName", GetType().Name).Error(ex, "Dispose error _globakKeyboardHook");
                    }
                    if (_globakScreenHook != null)
                    {
                        try
                        {
                            _globakScreenHook.StopCapture();
                            _globakScreenHook.ScreenEvent -= ScreenHookEventHandler;
                            _globakScreenHook.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.ForContext("FileName", GetType().Name).Error(ex, "Dispose error _globakScreenHook");
                        }
                    }

                    try
                    {
                        if(_remoteClient != null)
                        {
                            _remoteClient.LoginEvent -= LoginEventHandler;
                            _remoteClient.P2PConnectEvent -= P2PConnectEventHandler;
                            _remoteClient.ScreenEvent -= P2PScreenEventHandler;
                            _remoteClient.ChunksEvent -= P2PChunksEventHandler;
                            _remoteClient.ScreenSuccessEvent -= ScreenSuccessEventHandler;
                            _remoteClient.ChunksSuccessEvent -= ChunksSuccessEventHandler;
                            _remoteClient.MouseReceivedEvent -= MouseReceivedEventHandler;
                            _remoteClient.KeyboardReceivedEvent -= KeyboardReceivedEventHandler;
                            _remoteClient.P2PDisconnectedEvent -= P2PDisconnectedEventhandler;
                            _remoteClient.ClipboardReceivedEvent -= ClipboardReceivedEventHandler;

                            _remoteClient.Dispose();
                        }
                    }
                    catch(Exception ex)
                    {
                        Log.ForContext("FileName", GetType().Name).Error(ex, "Dispose error _remoteClient");
                    }

                    _connectionManager.Clear();
                }
            }
            _commandTasks = null;
            _screenTasks = null;
            _globakKeyboardHook = null;
            _globakScreenHook = null;
            _remoteClient = null;
            _isDisposed = true;
        }
        #endregion
    }
}
