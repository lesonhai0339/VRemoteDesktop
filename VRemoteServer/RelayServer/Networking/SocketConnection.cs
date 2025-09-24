using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Networking
{
    public class SocketConnection : IDisposable
    {
        private bool _receivedFirstPacket;
        private string _ip;
        private bool _disposed;
        private Socket _socket;
        private SocketAsyncEventArgs _readSocketAsyncEventArgs;
        private SocketAsyncEventArgs _sendSocketAsyncEventArgs;
        private DateTime _lastSendTime { get; set; }
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(TIMEOUT);
        private Timer _timer;
        private object _lockProperty = new object();
        private object _lockMethod = new object();

        //packet metadata
        private byte[] _currentHeader;
        private byte[] _remainingData;
        private int _dataExpected;
        private int _dataReceived;
        private string _id;

        public event EventHandler<SocketConnectionEventArg> SocketConnectionEvent;
        public SocketConnection(SocketAsyncEventArgs readSocketAsyncEventArgs, SocketAsyncEventArgs sendSocketAsyncEventArgs, Socket socket)
        {
            _receivedFirstPacket = false;
            _disposed = false;
            _lastSendTime = DateTime.Now; //init before check timeout
            _socket = socket;
            _readSocketAsyncEventArgs = readSocketAsyncEventArgs;
            _sendSocketAsyncEventArgs = sendSocketAsyncEventArgs;
            _readSocketAsyncEventArgs.UserToken = this;
            _sendSocketAsyncEventArgs.UserToken = this;
            _timer = new Timer(CheckTimeOut, null, TimeSpan.FromSeconds(SCHEDULE_TIME), TimeSpan.FromSeconds(SCHEDULE_TIME));
        }
        #region Properties
        public bool IsReceivedFirstPacket
        {
            get
            {
                lock (_lockProperty)
                {
                    return _receivedFirstPacket;
                }
            }
            set
            {
                lock (_lockProperty)
                {
                    _receivedFirstPacket = value;
                }
            }
        }
        public string IP
        {
            // if current ip is null, try to get it from RemoteEndPoint
            get
            {
                if (!string.IsNullOrEmpty(_ip))
                {
                    return _ip;
                }
                else if (_socket != null)
                {
                    return (_socket?.RemoteEndPoint as IPEndPoint)?.Address.ToString();
                }
                else
                {
                    return string.Empty;
                }         
            }
            private set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _ip = value;
                }
            }
        }
        public Socket Socket
        {
            get
            {
                lock (_lockProperty)
                {
                    return _socket;
                }
            }
            set
            {
                lock (_lockProperty)
                {
                    _socket = value;
                }
            }
        }
        /// <summary>
        /// It's <see cref="SocketAsyncEventArgs"/>
        /// </summary>
        public SocketAsyncEventArgs Reader
        {
            get
            {
                lock (_lockProperty)
                {
                    return _readSocketAsyncEventArgs;
                }
            }
            set
            {
                lock (_lockProperty)
                {
                    _readSocketAsyncEventArgs = value;
                }
            }
        }
        public SocketAsyncEventArgs Sender
        {
            get
            {
                lock (_lockProperty)
                {
                    return _sendSocketAsyncEventArgs;
                }
            }
            set
            {
                lock (_lockProperty)
                {
                    _sendSocketAsyncEventArgs = value;
                }
            }
        }
        #endregion
        #region Methods
        private bool CheckAlive()
        {
            try
            {
                // Check if socket is connected and not in an error state
                if (_socket == null || !_socket.Connected)
                    return false;

                // Use a shorter poll time (1000 microseconds = 1ms) and check both conditions
                bool hasDataToRead = _socket.Poll(1000, SelectMode.SelectRead);
                bool hasAvailableData = _socket.Available > 0;

                // Socket is alive if:
                // - It has data to read AND available data > 0, OR
                // - It doesn't have data to read (normal idle state)
                return !hasDataToRead || hasAvailableData;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
        private void CheckTimeOut(object obj)
        {
            var timer = DateTime.Now - _lastSendTime;
            if (timer > _timeout)
            {
                Log.ForContext("SocketConnectionIP", IP)
                   .Warning("Client has been idle for too long, disconnecting...");
                Dispose();
            }
            else if (!CheckAlive())
            {
                Log.ForContext("SocketConnectionIP", IP)
                   .Warning("Client is not connected anymore, disconnecting...");
                Dispose();
            }
        }

        private void ProcessData(string id, SocketDataType command, byte[] buffer)
        {
            SocketConnectionEvent?.Invoke(this, new SocketConnectionEventArg(SocketConnectionEventType.Data, id, command, buffer));
        }
        public void CalCuLateData(int offset, int dataLength)
        {
            lock (_lockMethod)
            {
                if (_readSocketAsyncEventArgs.Buffer == null)
                    return;
                if (_remainingData == null)
                    _remainingData = new byte[0];

                byte[] totalData = new byte[_remainingData.Length + dataLength];
                Buffer.BlockCopy(_remainingData, 0, totalData, 0, _remainingData.Length);
                Buffer.BlockCopy(_readSocketAsyncEventArgs.Buffer, offset, totalData, _remainingData.Length, dataLength);

                int bytesProcessed = 0;

                while (bytesProcessed < totalData.Length)
                {
                    if (_currentHeader == null)
                    {
                        if (totalData.Length - bytesProcessed >= PACKET_HEADER_LENGTH)
                        {
                            _currentHeader = new byte[PACKET_HEADER_LENGTH];
                            Buffer.BlockCopy(totalData, bytesProcessed, _currentHeader, 0, PACKET_HEADER_LENGTH);

                            _dataExpected = BitConverter.ToInt32(_currentHeader, PACKET_SIZE_INDEX);
                            _id = Encoding.ASCII.GetString(_currentHeader, PACKET_ID_INDEX, PACKET_ID_LENGTH);
                            _dataReceived = 0;
                        }
                        else
                        {
                            break;
                        }

                    }
                    if (_currentHeader != null)
                    {
                        SocketDataType type = (SocketDataType)_currentHeader[PACKET_TYPE_INDEX];
                        //command packet
                        if (_dataExpected == 0)
                        {
                            ProcessData(_id, type, new byte[0]);
                            _dataExpected = 0;
                            _currentHeader = null;
                            bytesProcessed += PACKET_HEADER_LENGTH;
                        }
                        else
                        {
                            int remainingDataNeeded = _dataExpected - _dataReceived;
                            int availableData = totalData.Length - bytesProcessed;
                            int dataNeedToReceive = Math.Min(remainingDataNeeded, availableData);

                            if (dataNeedToReceive > 0)
                            {
                                byte[] bytes = new byte[dataNeedToReceive];
                                Buffer.BlockCopy(totalData, bytesProcessed, bytes, 0, dataNeedToReceive);

                                _dataReceived += dataNeedToReceive;
                                bytesProcessed += dataNeedToReceive;


                                ProcessData(_id, type, bytes);
                                if (_dataReceived >= _dataExpected)
                                {
                                    _dataExpected = 0;
                                    _dataReceived = 0;
                                    _currentHeader = null;
                                    _id = null;
                                }
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                if (bytesProcessed < totalData.Length)
                {
                    int remainingBytes = totalData.Length - bytesProcessed;
                    _remainingData = new byte[remainingBytes];
                    Buffer.BlockCopy(totalData, bytesProcessed, _remainingData, 0, remainingBytes);
                }
                else
                {
                    _remainingData = Array.Empty<byte>();
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
            if (!disposing || _disposed) return;

            try
            {
                SocketConnectionEvent?.Invoke(this, new SocketConnectionEventArg(SocketConnectionEventType.Disconnected));

                try
                {
                    _socket?.Shutdown(SocketShutdown.Both);
                    _socket?.Close();
                }
                catch (Exception ex)
                {
                    Log.ForContext("SocketConnectionIP", IP)
                         .Error(ex, "An error occurred while disposing the client socket.");
                }

                _timer?.Dispose();
                _readSocketAsyncEventArgs?.Dispose();
                _sendSocketAsyncEventArgs?.Dispose();   
                _socket?.Dispose();
                _currentHeader = null;
                _remainingData = null;
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
