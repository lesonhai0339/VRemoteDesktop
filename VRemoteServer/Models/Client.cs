using Serilog;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.Utils;
using static VRemoteServer.Utils.Enums;

namespace VRemoteServer.Models
{
    public class Client : IDisposable
    {
        private string _ip;
        private bool _isDisposed = false;
        public Socket Socket { get; set; }
        public DateTime _lastSendTime { get; set; }
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(300);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Action<Client> _disconnectCallback;
        private Func<Enums.CommandType ,Client, byte[], Task<bool>> _dataCallback;


        //data
        private byte[] _currentHeader;
        private byte[] _remainingData;
        private int _dataExpected;
        private int _dataReceived;

        public Client(Socket socket, Action<Client> disconnectCallback, Func<Enums.CommandType, Client, byte[], Task<bool>> dataCallback)
        {
            _lastSendTime = DateTime.Now; //init before check timeout

            Socket = socket;
            _disconnectCallback = disconnectCallback;
            _dataCallback = dataCallback;
            CheckTimeOut();
        }
        #region Properties
        public string IP
        {
            // if current ip is null, try to get it from RemoteEndPoint
            get => _ip ??= (Socket.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Unknown";
            private set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _ip = "Unknown";
                }
                else
                {
                    _ip = value;
                }
            }
        }
        #endregion
        #region Methods
        private bool CheckAlive()
        {
            try
            {
                bool part = Socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = Socket.Available == 0;
                if (part && part2)
                {
                    return false; // Socket is disconnected
                }
                else
                {
                    return true; // Socket is connected
                }
            }
            catch (SocketException)
            {
                return false; // Socket is disconnected
            }
            catch (ObjectDisposedException)
            {
                return false; // Socket is disposed
            }
        }
        private void CheckTimeOut()
        {
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var timer = DateTime.Now - _lastSendTime;
                    if (timer > _timeout)
                    {
                        Log.ForContext("FileName", "Clients").Warning("Client {ClientId} has been idle for too long, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                        Dispose();
                        break;
                    }
                    if (!CheckAlive())
                    {
                        Log.ForContext("FileName", "Clients").Warning("Client {ClientId} is not connected anymore, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                        Dispose();
                        break;
                    }
                    try
                    {
                        await Task.Delay(10000, _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
        }
        private Task<int> ReceiveAsync(byte[] buffer, int offset, int size, SocketFlags flag)
        {
            var tcs = new TaskCompletionSource<int>();
            Socket.BeginReceive(buffer, offset, size, flag, ar =>
            {
                try
                {
                    tcs.TrySetResult(Socket.EndReceive(ar));
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, state: null);
            return tcs.Task;
        }
        private async Task ProcessData(Enums.CommandType command, byte[] buffer)
        {
            if (_dataCallback != null)
            {
                await _dataCallback(command,this, buffer);
            }
        }
        public async Task StartReceiving(int bufferSize = 8192)
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
                    try
                    {
                        int bytesRead = await ReceiveAsync(buffer, 0, bufferSize, SocketFlags.None);
                        if (bytesRead == 0) break;

                        Console.WriteLine("Received " + bytesRead + " bytes: ");
                        if(bytesRead == 5)
                        {
                            Console.WriteLine(BitConverter.ToString(buffer.Take(5).ToArray()) + "\n");
                        }

                        _lastSendTime = DateTime.Now;
                        byte[] data = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);
                        //cannot use fire-and-forger because packet order may be messy
                        await CaculateData(data);
                    }
                    catch
                    {
                        //Log.ForContext("FileName", "Clients").Error("Error when processing data from client {ClientId}", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
                    }
                }
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "Clients").Error($"Client IP:{IP} disconnected");
            }
            catch (OperationCanceledException)
            {
                Log.ForContext("FileName", "Clients").Error("Receiving cancelled for client {ClientId}", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "Clients").Error(ex, "An unexpected error occurred while receiving data.");
            }
            finally
            {
                Log.ForContext("FileName", "Clients").Information($"End receive on socket {(Socket.RemoteEndPoint as IPEndPoint)?.Address.ToString()}");
                Dispose();
            }
        }
        private async Task CaculateData(byte[] data)
        {
            if (_remainingData == null)
                _remainingData = new byte[0];

            byte[] totalData = new byte[_remainingData.Length + data.Length];
            Buffer.BlockCopy(_remainingData, 0, totalData, 0, _remainingData.Length);
            Buffer.BlockCopy(data, 0, totalData, _remainingData.Length, data.Length);
            
            int bytesProcessed = 0; 

            while(bytesProcessed < totalData.Length)
            {
                if (_currentHeader == null)
                {
                    if (totalData.Length - bytesProcessed >= 5)
                    {
                        //4 first byte is data length, 1 last byte is command type
                        _currentHeader = new byte[5];
                        Buffer.BlockCopy(totalData, bytesProcessed, _currentHeader, 0, 5);

                        _dataExpected = BitConverter.ToInt32(_currentHeader, 0);
                        //bytesProcessed += 5; //header
                        _dataReceived = 0;
                    }
                    else
                    {
                        break;
                    }

                }
                if (_currentHeader != null)
                {
                    //only command, not data send
                    if(_dataExpected == 0)
                    {
                        await ProcessData((Enums.CommandType)_currentHeader[4], new byte[0]);
                        _dataExpected = 0;
                        _currentHeader = null;
                        bytesProcessed += 5;
                    }
                    else
                    {
                        int remainingDataNeeded = _dataExpected - _dataReceived;
                        int avaiblebleData = totalData.Length - bytesProcessed;
                        int dataNeedtoReceive = Math.Min(remainingDataNeeded, avaiblebleData);

                        if (dataNeedtoReceive > 0)
                        {
                            byte[] bytes = new byte[dataNeedtoReceive];
                            Buffer.BlockCopy(totalData, bytesProcessed, bytes, 0, dataNeedtoReceive);

                            _dataReceived += dataNeedtoReceive;
                            bytesProcessed += dataNeedtoReceive;

                            Console.WriteLine($"Processing data: {_dataReceived}/{_dataExpected} bytes received");

                            await ProcessData((Enums.CommandType)_currentHeader[4], bytes);
                            if (_dataReceived >= _dataExpected)
                            {
                                Console.WriteLine($"Complete {_dataExpected} - {_dataReceived}");
                                Console.WriteLine("-------------------------------\n");
                                _dataExpected = 0;
                                _dataReceived = 0;
                                _currentHeader = null;
                            }
                        }
                    }
                }
                else
                {
                    break;
                }
            }
            if(bytesProcessed < totalData.Length)
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
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                try
                {
                    _cts.Cancel();
                    Socket?.Shutdown(SocketShutdown.Both);
                    Socket?.Close();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "An error occurred while disposing the client socket.");
                }
                finally
                {
                    _dataCallback = null;
                    Socket?.Dispose();
                    _disconnectCallback?.Invoke(this);
                }
            }
        }
        #endregion
    }
}
