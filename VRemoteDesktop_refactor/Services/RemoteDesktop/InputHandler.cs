using System;
using System.Diagnostics;
using System.Text;
using Vsign4.VRemoteDesktop_Refactor.Events;
using Vsign4.VRemoteDesktop_Refactor.Services.Keyboard;
using Vsign4.VRemoteDesktop_Refactor.Services.Keyboard.Enums;
using Vsign4.VRemoteDesktop_Refactor.Services.Mouse;
using Vsign4.VRemoteDesktop_Refactor.Services.SessionManagement;
using Vsign4.VRemoteDesktop_Refactor.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop_Refactor.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop_Refactor.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop_Refactor.Utils;

namespace Vsign4.VRemoteDesktop_Refactor.Services.RemoteDesktop
{
    /// <summary>
    /// Handles all local input (keyboard/mouse/clipboard) for the controlled machine,
    /// and forwards keyboard events captured on the controller machine.
    /// Single Responsibility: input simulation and forwarding only.
    /// </summary>
    internal sealed class InputHandler : IDisposable
    {
        private readonly IKeyboardService _keyboard;
        private readonly ISessionManager _sessions;

        /// <summary>
        /// Raised when a local key-press should be forwarded to the View
        /// (e.g. Ctrl+C triggers clipboard read on the controller side).
        /// </summary>
        public event EventHandler<KeyboardEventArgs> KeyboardForward;

        public InputHandler(IKeyboardService keyboard, ISessionManager sessions)
        {
            _keyboard = keyboard;
            _sessions = sessions;
            _keyboard.KeyPressed += OnLocalKeyPressed;
        }

        // ── Keyboard listener lifecycle ───────────────────────────────────────

        public void StartKeyboardListener(uint handle = 0)
        {
            uint h = handle != 0 ? handle : (uint)Process.GetCurrentProcess().Id;
            _keyboard.Start(h);
        }

        public void StopKeyboardListener() => _keyboard.Stop();

        public void AddHook(IntPtr handle)    => _keyboard.AddHook(handle);
        public void RemoveHook(IntPtr handle) => _keyboard.RemoveHook(handle);

        // ── Incoming events from controller session ────────────────────────────

        public void HandleMouse(int screenWidth, int screenHeight, byte[] data)
        {
            var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(data, screenWidth, screenHeight);
            if (mouseEvent == null) return;

            if (!VirtualMouse.MouseEvent(mouseEvent))
                Logger.Log.ForContext("FileName", nameof(InputHandler)).Error("Failed to simulate mouse event");
        }

        public void HandleKeyboard(ClientSessionDataReceivedEventArgs e)
        {
            var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(e.Data);
            if (keyEvent == null) return;
            VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);
        }

        public void HandleClipboard(ClientSessionDataReceivedEventArgs e) => SetClipboard(e.Data);

        // ── Clipboard utilities ───────────────────────────────────────────────

        public string GetClipboard() => VirtualClipboard.GetClipboardString();

        public bool SetClipboard(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);
            byte[] unicode = Encoding.Unicode.GetBytes(text + '\0');
            return VirtualClipboard.SetClipboard(unicode, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
        }

        public bool SetClipboard(byte[] data, int index, int length)
        {
            byte[] slice = new byte[length];
            Buffer.BlockCopy(data, index, slice, 0, length);
            return VirtualClipboard.SetClipboard(slice, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
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

        // ── Local keyboard hook ───────────────────────────────────────────────

        private void OnLocalKeyPressed(object sender, KeyboardEventArgs e)
        {
            byte[] clipboardBytes;
            SocketDataType type;

            if (CheckClipboard(e, out clipboardBytes, out type))
            {
                // Broadcast clipboard content to all controlled sessions
                foreach (var session in _sessions.Connections)
                {
                    if (session.SessionType == ClientType.Controlled)
                    {
                        session.AddWork(QueuePriority.High, new TaskObject
                        {
                            TaskType = type,
                            Data = clipboardBytes,
                            SessionId = session.SessionId,
                            IsSendHeader = true,
                        });
                    }
                }
            }
            else
            {
                var handler = KeyboardForward;
                if (handler != null) handler.Invoke(sender, e);
            }
        }

        public void Dispose()
        {
            _keyboard.KeyPressed -= OnLocalKeyPressed;
        }
    }
}
