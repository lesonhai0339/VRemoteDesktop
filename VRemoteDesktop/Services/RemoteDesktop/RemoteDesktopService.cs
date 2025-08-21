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
using VRemoteDesktop.Services.Mouse;
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
            return _globalHook.GetClipboard(); ;
        }
        public bool SetClipboard(byte[] data)
        {
            return _globalHook.SetClipboard(data); ;
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            return _globalHook.SetClipboard(data, index, length); ;
        }
        public void StartScreenCapture()
        {
            _globalHook.StartScreenCapture();
        }
        public void StopScreenCapture()
        {
            _globalHook.StopScreenCapture();
        }
        public void RemoveClientById(string id)
        {
            _vClientManager.Remove(id);
            if (_vClientManager.Connections.Count == 0)
            {
                StopKeyboardListener();

                if (!_vClientManager.HasClientOfType(VClientType.Receiver))
                    StopScreenCapture();
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
            if (_vClientManager.Connections.Count > 0)
            {
                StartKeyboardListener();

                if (_vClientManager.HasClientOfType(VClientType.Receiver))
                    StartScreenCapture();
            }
            return newClient;
        }
        public ConcurrentDictionary<string, VClient> GetClients()
        {
            return _vClientManager.Connections;
        }
        private void P2PRequestConnectHandler(object sender, P2PClientDataReceived e)
        {
            if (_clientInfo.IsAuthenticated(e.Data, out ClientInfo partnerInfo, out string connectionId))
            {
                //P2P request connect succeeeded
                _vClientManager.AcceptP2PConnect(_clientInfo.GetMyInfo(), partnerInfo, connectionId);

                if (_vClientManager.HasClientOfType(VClientType.Receiver))
                    StartScreenCapture();
            }
            else
            {
                //P2P request connect failed
                _vClientManager.RejectP2PConnect(sender, e.Data);
            }
        }
        private void SendScreenChangedToClient(object sender, ScreenCaptureEventArgs e)
        {
            _vClientManager.ScreenUpdate(e);
        }
        private void MouseReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                var me = GetMe();
                var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(e.Data, me.Width, me.Height);
                bool flag = VirtualMouse.MouseEvent(mouseEvent);
                if (!flag)
                {
                    Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error("Mouse event failed");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error(ex, "Error processing mouse data");
            }
        }
        private void KeyboardReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(e.Data);
                VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(KeyboardReceivedEventHandler)).Error(ex, "Error processing keyboard data");
            }
        }
        private void ProcessP2PDisconnect(object sender, P2PClientDataReceived e)
        {
            if (sender is VClient client)
            {
                RemoveClientById(client.SocketId);
            }
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
                case DataType.Mouse:
                    MouseReceivedEventHandler(sender, e);
                    break;
                case DataType.Keyboard:
                    KeyboardReceivedEventHandler(sender, e);
                    break;
                case DataType.P2PDisconnect:
                    ProcessP2PDisconnect(sender, e);
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
