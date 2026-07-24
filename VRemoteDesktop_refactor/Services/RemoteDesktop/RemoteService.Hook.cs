using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.Keyboard;
using Vsign4.VRemoteDesktop.Services.Keyboard.Enums;
using Vsign4.VRemoteDesktop.Services.Mouse;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Events;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
    {
        #region Hook

        #region Screen
        private void InitCapturingBuffer()
        {
            try
            {
                _screenSender.InitializeSenderComponents();
            }
            catch (Exception ex)
            {
                //Important error
                Logger.Log.ForContext("FileName", "RemoteService_Hook").Error(ex, "InitCapturingBuffer err ");
            }
        }
        private void StartScreenCapture()
        {
            if (_screenSender.State == 0) // 0 = idle, 1 = running, 2 = stopping
            {
                _screenSender.Start();
            }
        }
        private void StopScreenCapture()
        {
            if (_screenSender.State == 1) // 0 = idle, 1 = running, 2 = stopping
            {
                _screenSender.Stop();
            }
        }
        private void FullScreenSendCompleted(object sender, EventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                _screenSender.AddSessionBuffer(clientSession.SessionId, clientSession.BufferSwapper);
            }
        }
        private void OnRegionEventHandler(object sender, FrameEventArgs e)
        {
            try
            {
                _sessionManager.ScreenCapture(SessionManagement.Enums.ClientType.Controlled, e.Type);

            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "OnDirtyRegionError").Error(ex.Message);
            }
        }
        #endregion

        #region Mouse
        public void MouseReceivedEventHandler(int width, int height, byte[] data)
        {
            var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(data, width, height);
            if (mouseEvent == null)
                return;

            bool flag = VirtualMouse.MouseEvent(mouseEvent);
            if (!flag)
            {
                Logger.Log.ForContext("Filename", GetType().Name).Error("Failed handler mouse event");
            }
        }

        private void MouseReceived(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var bounds = _machineProfile.Bounds;
            MouseReceivedEventHandler(bounds.Width, bounds.Height, e.Data);
        }
        #endregion

        #region Keyboard
        public void StartKeyboardListener(uint handle = 0)
        {
            if (handle == 0)
            {
                uint h = (uint)Process.GetCurrentProcess().Id;
                _keyboardService.Start(h);
            }
            else
            {
                _keyboardService.Start(handle);
            }
        }
        public void StopKeyboardListener()
        {
            _keyboardService.Stop();
        }
        public void AddKeyboardHook(IntPtr handle)
        {
            _keyboardService.AddHook(handle);
        }
        public void RemoveKeyboardHook(IntPtr handle)
        {
            _keyboardService.RemoveHook(handle);
        }

        public void KeyboardReceivedEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(e.Data);
            if (keyEvent == null)
                return;
            VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);
        }
        private void KeyPressedEventHandler(object sender, KeyboardEventArgs e)
        {
            byte[] data;
            SocketDataType type;
            if (CheckClipboard(e, out data, out type))
            {
                foreach (var connection in _sessionManager.Connections)
                {
                    if (connection.SessionType == SessionManagement.Enums.ClientType.Controlled)
                        connection.AddWork(QueuePriority.High, new TaskObject
                        {
                            TaskType = type,
                            Data = data,
                            SessionId = connection.SessionId,
                            IsSendHeader = true,
                            ChunkFileInfo = null
                        });
                }
            }
            else
            {
                if(OnSessionKeyboard != null)
                    OnSessionKeyboard.Invoke(sender, e);
            }
        }
        #endregion

        #region Clipboard
        private void ClipboardReceived(object sender, ClientSessionDataReceivedEventArgs e)
        {
            SetClipboard(e.Data);
        }
        public string GetClipboard()
        {
            return VirtualClipboard.GetClipboardString();
        }
        public bool CheckClipboard(KeyboardEventArgs e, out byte[] clipboardBytes, out SocketDataType type)
        {
            clipboardBytes = null;
            type = SocketDataType.None;
            if (e.Combination == KeyCombination.Copy && e.Handle == IntPtr.Zero && e.IsSynthetic)
            {
                type = SocketDataType.ClipboardSend;
                clipboardBytes = Encoding.UTF8.GetBytes(GetClipboard());
                return true;
            }
            return false;
        }
        public bool SetClipboard(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);

            byte[] unicodeData = Encoding.Unicode.GetBytes(text + '\0');

            return VirtualClipboard.SetClipboard(unicodeData, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            byte[] clipboardData = new byte[length];
            Buffer.BlockCopy(data, index, clipboardData, 0, length);
            return VirtualClipboard.SetClipboard(clipboardData, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
        }
        #endregion
        #endregion
    }
}
