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


        public event Action<bool> LoginEvent;

        public RemoteDesktopService() 
        {
            OwnerInfo = Extensions.InitInfo();

            _resetEvent = new ManualResetEvent(false);

            RemoteClient = new RemoteClient(OwnerInfo);
            KeyboardHook = new GlobalKeyboardHook();

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

                    }
                    _globakKeyboardHook = value;
                    if(_globakKeyboardHook != null)
                    {

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
                    }
                    _remoteClient = value;
                    if (_remoteClient != null)
                    {
                        _remoteClient.ConnectEventHandler += ConnectEventHandler;
                        _remoteClient.LoginEventHandler += LoginEventHandler;

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
                screenHeader[5] = (byte)e.Type;

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
