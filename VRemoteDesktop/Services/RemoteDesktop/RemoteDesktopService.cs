using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public class RemoteDesktopService : IDisposable
    {
        private volatile bool _disposed;

        private readonly IClientInfoManager _clientInfo;
        private readonly GlobalHookService _globalHook;
        private readonly VClientManager _vClientManager;

        public event EventHandler<KeyboardEventArgs> KeyboardEvent;
        public event EventHandler<P2PClientDataReceived> DataReceivedEvent;
        public RemoteDesktopService(GlobalHookService globalHook, VClientManager vClientManager, IClientInfoManager clientInfo)
        {
            _disposed = false;
            _clientInfo = clientInfo;

            _globalHook = globalHook;
            _vClientManager = vClientManager;


            _globalHook.ScreenCaptureChanged += ScreenCaptureEventHandler;
            _globalHook.KeyboardReceived += KeyboardEventHandler;
            _vClientManager.ClientDataReceived += ClientDataReceivedEventHandler;
        }

        #region Properties
        public bool Disposed => _disposed;
        #endregion
        #region Methods
        public ClientInfo GetMe()
        {
            return _clientInfo.GetMyInfo();
        }
        public void UpdateMyInfo(byte[] data)
        {
            _clientInfo.UpdateMyInfo(data);
        }
        public void StartKeyboardListener()
        {
            _globalHook.StartKeyboardListener();
        }
        public void StopKeyboardListener()
        {
            _globalHook.StopKeyboardListener();
        }
        public void AddKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            _globalHook.AddKeyboardHook(handle);
        }
        public void RemoveKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            _globalHook.RemoveKeyboardHook(handle);
        }
        public string GetClipboardString()
        {
            string clipboard = _globalHook.GetClipboard();
            if(!string.IsNullOrEmpty(clipboard))
                return clipboard;
            return null;
        }
        public bool SetClipboard(byte[] data)
        {
            bool isSucceeded = _globalHook.SetClipboard(data);
            return isSucceeded;
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            bool isSucceeded = _globalHook.SetClipboard(data, index, length);
            return isSucceeded;
        }
        public void StartScreenCapture()
        {
            _globalHook.StartScreenCapture();
        }
        public void StopScreenCapture()
        {
            _globalHook.StopScreenCapture();
        }
        public void AddClient(string id, VClient client)
        {
            _vClientManager.Add(id, client);
            if(_vClientManager.Connections.Count > 0)
            {
                StartKeyboardListener();
                bool hasReceiver = _vClientManager.Connections.Any(x => x.Value.ClientType == VClientType.Receiver);
                if(hasReceiver)
                    StartScreenCapture();
            }
        }
        public void RemoveClientById(string id)
        {
            _vClientManager.Remove(id);
            if (_vClientManager.Connections.Count == 0)
            {
                StopKeyboardListener();
                //StopScreenCapture();
            }
        }
        public VClient GetClientById(string id)
        {
            var client = _vClientManager.GetByKey(id);
            return client;
        }
        public VClient NewClient(string id, VClientType type)
        {
            var newClient = _vClientManager.New(id, type);
            return newClient;
        }
        public ConcurrentDictionary<string, VClient> GetClients()
        {
            return _vClientManager.Connections;
        }
        private void P2PRequestConnectHandler(object sender, P2PClientDataReceived e)
        {
            _vClientManager.AcceptP2PConnect(_clientInfo.GetMyInfo().ToNetworkString(), e.Data);
        }
        private void SendScreenChangedToClient(object sender, ScreenCaptureEventArgs e)
        {
            _vClientManager.ScreenUpdate(e);
        }
        #endregion
        #region Events
        private void ScreenCaptureEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            SendScreenChangedToClient(sender, e);
        }
        private void KeyboardEventHandler(object sender, KeyboardEventArgs e)
        {
            KeyboardEvent?.Invoke(sender, e);
        }
        private void ClientDataReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            switch (e.Type)
            {
                case DataType.Clipboard:
                    SetClipboard(e.Data);
                    break;
                case DataType.P2PRequestConnect:
                    P2PRequestConnectHandler(sender, e);
                    break;
                default:
                    DataReceivedEvent?.Invoke(sender, e);
                    break;
            }
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if(_globalHook != null)
                    {
                        _globalHook.ScreenCaptureChanged -= ScreenCaptureEventHandler;
                        _globalHook.KeyboardReceived -= KeyboardEventHandler;
                        _globalHook.Dispose();
                    }
                    if(_vClientManager != null)
                    {
                        _vClientManager.ClientDataReceived -= ClientDataReceivedEventHandler;
                        _vClientManager.Dispose();
                    }
                }
            }
        }
    }
}
