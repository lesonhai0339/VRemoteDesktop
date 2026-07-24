using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSocket;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement
{
    public class ClientSocket : IDisposable
    {
        // Upper bound for a single framed packet. A full-screen raw frame on a 4K display is
        // ~16 MB; 64 MB leaves generous head-room while still rejecting corrupt/huge lengths.
        private const int MAX_FRAME_SIZE = 64 * 1024 * 1024;

        private int _disposed = 0;
        private string _sessionId;
        private bool _isSocketConnected;
        private Socket _socket;
        private AutoResetEvent _sckConnect;
        private CancellationTokenSource _cancellationTokenSource;

        public event EventHandler<SocketDataReceivedEventArgs> OnDataReceived;
        public event EventHandler<SocketDisconnectedEventArgs> OnDisconnected;
        public event EventHandler<SocketConnectedEventArgs> OnConnected;
        public event EventHandler<SocketSendCompletedEventArgs> OnSendCompleted;
        public ClientSocket(string sessionId, CancellationToken token)
        {
            _sessionId = sessionId;
            _sckConnect = new AutoResetEvent(false);

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        }
        #region Properties
        public Socket Socket
        {
            get
            {
                return _socket;
            }
            set
            {
                _socket = value;
            }
        }
        public bool SocketConnected
        {
            get
            {
                return _isSocketConnected;
            }
            private set
            {
                _isSocketConnected = value;
            }
        }

        #endregion
        #region Methods
        public bool TryConnect(string ip, int port, int retry = 0, int waitRespondTime = 3000)
        {
            bool respond;
            int count = 0;
            while (count <= retry)
            {
                respond = Connect(ip, port, waitRespondTime);
                if (respond)
                    return true;
                count++;
            }
            return false;
        }
        /// <summary>
        /// Connect to remote server with default IP and port
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        private bool Connect(string ip, int port, int timeout = 3000)
        {
            try
            {
                _sckConnect.Reset();
                if (string.IsNullOrWhiteSpace(ip) || port < 0)
                {
                    return false;
                }

                IPAddress validIp;
                IPEndPoint remoteEP;
                if (IPAddress.TryParse(ip, out validIp))
                {
                    remoteEP = new IPEndPoint(validIp, port);

                    if (_socket == null || !_socket.Connected)
                    {
                        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    }
                    _socket.SendBufferSize = 65536;
                    _socket.ReceiveBufferSize = 65536;
                    _socket.NoDelay = true;
                    _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                    _socket.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), _socket);
                    bool respond = _sckConnect.WaitOne(timeout);
                    return respond;
                }
                else
                {
                    Logger.Log.ForContext("FileName", "Connect").Error("Invalid IP address: {Ip}", ip);
                }
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", "Connect").Error(ex, "Error when connect to relay server");
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "Connect").Error(ex, "Unexpected error when connect to relay server");
            }
            return false;
        }
        public bool Listen(int port, int timeout)
        {
            try
            {
                EndPoint endpoint = new IPEndPoint(IPAddress.Any, port);

                if (Socket == null || !Socket.Connected)
                {
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    Socket.NoDelay = true;
                }
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                _socket.Bind(endpoint);
                _socket.Listen(1);
                _socket.BeginAccept(ListenCallback, _socket);
                bool flag = _sckConnect.WaitOne(timeout);
                return flag;
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Listen error on port " + port + " for socketid: " + _sessionId);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when starting to listen on port " + port);
                return false;
            }
        }
        private void ListenCallback(IAsyncResult ar)
        {
            try
            {
                var sck = ar.AsyncState as Socket;
                var client = sck.EndAccept(ar);

                //end listen
                sck.Close();
                sck.Dispose();

                _socket = client;

                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = _socket;

                _socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when connecting to remote server");
            }
            finally
            {
                try { _sckConnect.Set(); } catch { }

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
                if (Socket == null)
                    return;

                _socket.EndConnect(ar);
                if (!_socket.Connected)
                {
                    if (OnConnected != null)
                        OnConnected.Invoke(this, new SocketConnectedEventArgs(_sessionId, false));
                    return;
                }
                //Connected
                SocketConnected = true;
                if (OnConnected != null)
                {
                    OnConnected.Invoke(this, new SocketConnectedEventArgs(_sessionId, true));
                }
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = _socket;
                stateObject.SckId = _sessionId;

                _socket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SocketException when connecting to remote server");
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when connecting to remote server");
            }
            finally
            {
                if(_socket != null)
                    _sckConnect.Set();
            }
        }
        private void DataCallback(IAsyncResult ar)
        {
            var cts = _cancellationTokenSource;
            if (cts == null) return;
            try
            {
                var token = cts.Token;
                StateObject stateObject = (StateObject)ar.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(ar);
                if (num == 0)
                {
                    var handlerErr = OnDisconnected;
                    if (handlerErr != null)
                        handlerErr.Invoke(this, new SocketDisconnectedEventArgs(_sessionId));
                    //Socket disconnect, dispose this class
                    //this.Dispose();
                    return;
                }

                if (num > 0)
                {
                    stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0, num);

                    while (token != null && !token.IsCancellationRequested)
                    {
                        if (!(stateObject.ByteArrayBuilder.Length >= 5))
                        {
                            break;
                        }

                        int length = BitConverter.ToInt32(stateObject.ByteArrayBuilder.lsByte.GetRange(0, 4).ToArray(), 0);

                        // Guard against a corrupt frame length. Without this a negative value
                        // passes the "Length >= length" check (Length is always >= 0) and then
                        // Cut(length) -> GetRange(0, negative) throws, killing the receive loop
                        // (BeginReceive is never re-armed => the connection silently dies).
                        if (length < HeaderSchema.TotalHeaderSize || length > MAX_FRAME_SIZE)
                        {
                            Logger.Log.ForContext("FileName", this.GetType().Name).Error("Corrupt frame length " + length + " on socketid " + _sessionId + ", dropping connection");
                            var handlerCorrupt = OnDisconnected;
                            if (handlerCorrupt != null)
                                handlerCorrupt.Invoke(this, new SocketDisconnectedEventArgs(_sessionId));
                            return;
                        }

                        if (!(stateObject.ByteArrayBuilder.Length >= length))
                        {
                            break;
                        }
                        Array src = stateObject.ByteArrayBuilder.Cut(length).ToArray();
                        byte[] data = new byte[length];
                        Buffer.BlockCopy(src, 0, data, 0, data.Length);
                        ProcessReceiveData(data);

                        if (token.IsCancellationRequested)
                            break;
                    }
                }
                try
                {
                    if (!token.IsCancellationRequested)
                        Socket.BeginReceive(stateObject.Buffer, 0, stateObject.Buffer.Length, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
                }
                catch (SocketException ex)
                {
                    Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Begin receive error");
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "Unexpected error when receiving data from remote server");
            }
        }
        private void ProcessReceiveData(byte[] bytes)
        {
            try
            {
                //13 bytes header
                int headerSize = HeaderSchema.TotalHeaderSize;

                if (bytes.Length < headerSize)
                {
                    return;
                }
                int offset = 0;

                int dataLength = BitConverter.ToInt32(bytes, offset);
                if (dataLength <= 0)
                {
                    return;
                }
                offset += HeaderSchema.PayloadBytes;

                SocketDataType dataType = (SocketDataType)bytes[offset];
                if (!Enum.IsDefined(typeof(SocketDataType), dataType))
                {
                    return;
                }
                offset += HeaderSchema.TypeBytes;

                var result = ByteArrayHelper.ConvertByteArrayToString(bytes, offset, HeaderSchema.IdBytes, EncodingType.ASCII);
                if (!result.IsSuccess)
                {
                    return;
                }
                string socketId = result.GetResult();
                offset += HeaderSchema.IdBytes;

                byte[] data = new byte[bytes.Length - headerSize];
                Buffer.BlockCopy(bytes, offset, data, 0, data.Length);

                if (OnDataReceived != null)
                {
                    OnDataReceived.Invoke(this, new SocketDataReceivedEventArgs(dataType, data));
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("", GetType().Name).Error(ex, "ProcessReceiveData error");
            }
        }
        public void Send(Sendstate state)
        {
            if (_socket == null)
                return;

            if (!_socket.Connected)
            {
                throw new InvalidOperationException("Socket with id: " + _sessionId + " no available");
            }

            if ((Environment.TickCount - state.Timeout) > HeaderSchema.Timeout * 1000)
            {
                throw new TimeoutException("Send timeout");
            }

            _socket.BeginSend(state.Data, state.Sent, state.Remained, SocketFlags.None, SendCallback, state);
        }
        public void Send(byte[] data)
        {
            if (_socket == null)
                return;

            if (!_socket.Connected)
            {
                throw new InvalidOperationException("Socket with id: " + _sessionId + " no available");
            }
            _socket.Send(data, SocketFlags.None);
        }
        /// <summary>
        /// Blocking, best-effort send that never throws. Used to push a final
        /// notification (e.g. Disconnect) to the partner while the socket is still alive.
        /// </summary>
        public bool SendSync(byte[] data)
        {
            var sck = _socket;
            if (sck == null || data == null || data.Length == 0)
                return false;
            try
            {
                if (!sck.Connected)
                    return false;
                int sent = 0;
                while (sent < data.Length)
                {
                    int n = sck.Send(data, sent, data.Length - sent, SocketFlags.None);
                    if (n <= 0)
                        return false;
                    sent += n;
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendSync error on socketid: " + _sessionId);
                return false;
            }
        }
        private void SendCallback(IAsyncResult ar)
        {
            var sentState = (Sendstate)ar.AsyncState;
            bool isFinal = false;
            bool hasError = false;
            try
            {
                checked
                {
                    int num = Socket.EndSend(ar);

                    if (num <= 0)
                    {
                        throw new InvalidOperationException("Send error on socket with socket Id: " + _sessionId);
                    }
                    sentState.Sent += num;
                    sentState.Remained -= num;
                    if (sentState.Remained > 0)
                    {

                        Send(sentState);
                    }
                    else
                    {
                        isFinal = true;
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback: socket error on socketid: " + _sessionId);

                isFinal = true;
                hasError = true;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", this.GetType().Name).Error(ex, "SendCallback error on socketid: " + _sessionId);
                isFinal = true;
                hasError = true;

            }
            finally
            {
                if (isFinal)
                {
                    var handlerCompleted = OnSendCompleted;
                    if (handlerCompleted != null)
                        handlerCompleted.Invoke(this, new SocketSendCompletedEventArgs(_sessionId, sentState));

                    if (hasError)
                    {
                        var handlerErr = OnDisconnected;
                        if (handlerErr != null)
                            handlerErr.Invoke(this, new SocketDisconnectedEventArgs(_sessionId));
                    }
                }
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
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            var cts = _cancellationTokenSource;
            if (cts != null)
            {
                _cancellationTokenSource = null;
                cts.Cancel();
                cts.Dispose();
            }

            if (disposing)
            {
                try
                {
                    var sck = _socket;
                    if (sck != null)
                    {
                        _socket = null;
                        try
                        {
                            if (sck.Connected)
                                sck.Shutdown(SocketShutdown.Both);
                        }
                        catch { }
                        sck.Close();
                    }
                }
                catch (Exception)
                {
                }
                _sckConnect.Dispose();
            }
        }
    }
}
