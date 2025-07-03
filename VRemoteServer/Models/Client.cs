using Serilog;
using System;
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
        private DateTime _lastSendTime { get; set; }
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(300);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private Action<Client> _disconnectCallback;
        private Func<Enums.CommandType ,Client, byte[], Task<bool>> _dataCallback;

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
                        Log.Warning("Client {ClientId} has been idle for too long, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                        Dispose();
                        break;
                    }
                    if (!CheckAlive())
                    {
                        Log.Warning("Client {ClientId} is not connected anymore, disconnecting...", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
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
        private async Task ProcessData(byte[] buffer, int length)
        {
            if (_dataCallback != null)
            {
                await _dataCallback((CommandType)buffer[0],this, buffer);
            }
        }
        public async Task StartReceiving(int bufferSize = 8192)
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[bufferSize];
                    int bytesRead = await ReceiveAsync(buffer, 0, bufferSize, SocketFlags.None);
                    if (bytesRead == 0) break;

                    _lastSendTime = DateTime.Now;
                    try
                    {
                        //cannot use fire-and-forger because packet order may be messy
                        await ProcessData(buffer, bytesRead);
                    }
                    catch
                    {
                        Log.Error("Error when processing data from client {ClientId}", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
                    }
                }
            }
            catch (SocketException ex)
            {
                Log.Error(ex, "Socket exception occurred while receiving data.");
            }
            catch (OperationCanceledException)
            {
                Log.Information("Receiving cancelled for client {ClientId}", Socket.RemoteEndPoint?.ToString() ?? "Unknown");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while receiving data.");
            }
            finally
            {
                Log.Information($"End receive on socket {(Socket.RemoteEndPoint as IPEndPoint)?.Address.ToString()}");
                Dispose();
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
