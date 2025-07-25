using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;

namespace VRemoteClient.Services
{
    public class RemoteDesktopService
    {
        private object _lockProperties = new object();

        private Thread _screenThread;

        private ClientInfo _ownerInfo;


        private GlobalKeyboardHook _globakKeyboardHook;
        private GlobalScreenHook _globakScreenHook;
        private RemoteClient _remoteClient;

        public RemoteDesktopService() 
        {
            OwnerInfo = Utils.Extensions.InitInfo();
            RemoteClient = new RemoteClient(OwnerInfo);
            KeyboardHook = new GlobalKeyboardHook();

            Task.Factory.StartNew(() =>
            {
                ScreenHook = new GlobalScreenHook();
            }, TaskCreationOptions.LongRunning);
        }

        #region Properties
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

                    }
                    _globakScreenHook = value;
                    if (_globakScreenHook != null)
                    {

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

                    }
                    _remoteClient = value;
                    if (_remoteClient != null)
                    {

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
        public void ConnectToServer()
        {
            RemoteClient.Connect();
        }
        public void Send(CommandType type, byte[] data, int length, bool includeHeader = true)
        {
            if (includeHeader)
            {
                RemoteClient.Send(type, data, includeHeader);
            }
            else
            {
                RemoteClient.Send(type, data, length);
            }
        }
        #endregion
        #region Events
        #endregion
    }
}
