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
using VRemoteClient.Utils;
using static VRemoteClient.Models.Enums.KeyboardEnums;

namespace VRemoteClient.Services
{
    public class RemoteDesktopService
    {
        private readonly object _lockProperties = new object();
        private volatile bool _isSocketConnectSuccess;

        private Thread _screenThread;
        private ManualResetEvent _resetEvent;

        private ClientInfo _ownerInfo;


        private GlobalKeyboardHook _globakKeyboardHook;
        private GlobalScreenHook _globakScreenHook;
        private RemoteClient _remoteClient;

        public event Action<bool> ConnectServerEvent;
        public event Action<bool> LoginEvent;
        public event Action<bool, ConnectionInfo> P2PConnectEvent;
        public event Action<object, KeyMessageEventArgs> KeyboardEvent;
        public event Action<byte[]> ScreenEvent;
        public event Action<List<ScreenBlock>> ChunksEvent;

        public RemoteDesktopService() 
        {
            OwnerInfo = Extensions.InitInfo();

            _resetEvent = new ManualResetEvent(false);
            InitializeCompoment();
        }
        public void InitializeCompoment()
        {
            if (KeyboardHook == null)
            {
                KeyboardHook = new GlobalKeyboardHook();
            }
            if(RemoteClient == null)
            {
                RemoteClient = new RemoteClient(OwnerInfo);

            }
            Task.Factory.StartNew(() =>
            {
                ScreenHook = new GlobalScreenHook();
            }, TaskCreationOptions.LongRunning);
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
        public GlobalScreenHook ScreenHook
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
                        _remoteClient.ConnectEventHandler -= ConnectEventHandler;
                        _remoteClient.LoginEventHandler -= LoginEventHandler;
                        _remoteClient.P2PConnectEventHandler -= P2PConnectEventHandler;
                        _remoteClient.P2PScreenEventHandler -= P2PScreenEventHandler;
                        _remoteClient.P2PChunksEventHandler -= P2PChunksEventHandler;
                    }
                    _remoteClient = value;
                    if (_remoteClient != null)
                    {
                        _remoteClient.ConnectEventHandler += ConnectEventHandler;
                        _remoteClient.LoginEventHandler += LoginEventHandler;
                        _remoteClient.P2PConnectEventHandler += P2PConnectEventHandler;
                        _remoteClient.P2PScreenEventHandler += P2PScreenEventHandler;
                        _remoteClient.P2PChunksEventHandler += P2PChunksEventHandler;
                    }
                }
            }
        }
        #endregion
        #region Methods
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
        public void StartScreenHook()
        {
            try
            {
                ScreenHook.StartCapture();
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Start screen hook failed");
            }
        }
        public void StopScreenHook()
        {
            try
            {
                ScreenHook.StopCapture();
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopService").Error(ex, "Stop screen hook failed");
            }
        }
        public void ConnectToServer(string ip, int port)
        {
            _resetEvent.Reset();
            try
            {
                RemoteClient.Connect(ip, port);
                bool flag = _resetEvent.WaitOne(5000);
                if (!flag)
                {
                    ConnectServerEvent?.Invoke(flag);
                    //TODO: invoke event form main from notify that login failed
                    Log.ForContext("Filename", this.GetType().Name).Error("Socket connect failed");
                    return;
                }
                Login();
            }
            catch(Exception ex)
            {
                Log.ForContext("Filename", this.GetType().Name).Error("ConnectToServer error");
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

                RemoteClient.AddWork(new TaskObject
                (
                    taskType: RemoteType.P2PConnect,
                    data: data
                ));
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "P2P connection error");
            }
        }

        public void AddWork(TaskObject task)
        {
            RemoteClient.AddWork(task);
        }
        public void AddWorkGroup(List<TaskObject> tasks)
        {
            RemoteClient.AddWorkGroup(tasks);
        }
        public string FormatKeyboardInput(IntPtr command, Keys modifier, Keys code, KeyState type)
        {
            return KeyboardHook.KeyboardEventTostring(command, modifier, code, type);
        }

        private void Login()
        {
            try
            {
                string data = Extensions.DataStringBuilder(new string[] { OwnerInfo.ToString() });
                byte[] dataBytes = Encoding.ASCII.GetBytes(data);
                RemoteClient.AddWork(new TaskObject
                (
                    taskType: RemoteType.Login,
                    data: dataBytes
                ));
            }
            catch(Exception ex)
            {
                Log.ForContext("Filename", this.GetType().Name).Error(ex, "Login error");
            }
        }
        #endregion
        #region Events
        /// <summary>
        /// Callback when connect socket to server successed
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
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
        private void P2PConnectEventHandler(bool flag, ConnectionInfo info)
        {
            P2PConnectEvent?.Invoke(flag, info);
        }
        private void P2PScreenEventHandler(byte[] screen)
        {
            ScreenEvent?.Invoke(screen);
        }
        private void P2PChunksEventHandler(List<ScreenBlock> blocks)
        {
            ChunksEvent?.Invoke(blocks);
        }
        private void KeyboardPressedEvent(object sender, KeyMessageEventArgs e)
        {
            KeyboardEvent?.Invoke(sender, e);
        }
        private void ScreenHookEventHandler(object sender, CustomScreenEventArgs e)
        {
            try
            {
                if (e.Data.Count == 0 || e.TotalSize == 0)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error("Screen missing some value");
                    return;
                }

                //header
                byte[] screenHeader = new byte[5];
                Buffer.BlockCopy(BitConverter.GetBytes(e.TotalSize + 5), 0, screenHeader, 0, 4);
                screenHeader[4] = (byte)e.Type;

                List<TaskObject> tasks = new List<TaskObject>();
                tasks.Add(new TaskObject(
                    taskType: e.Type,
                    data: screenHeader,
                    isSendHeader: false
                ));
                //data
                for (int i = 0; i < e.Data.Count; i++)
                {
                    var task = new TaskObject(
                        taskType: e.Type,
                        data: e.Data[i],
                        isSendHeader: false
                    );
                    tasks.Add(task);
                }
                RemoteClient.AddWorkGroup(tasks);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        #endregion
    }
}
