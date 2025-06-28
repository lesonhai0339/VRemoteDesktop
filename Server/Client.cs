using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteServer
{
    public class Client
    {
        public Socket Socket { get; private set; }
        public Func<Client, byte[], int, Task> Callback { get; set; }
        private readonly Action<Client> _isDisconnected;
        public DateTime _lastSendTime;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(300);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        public Client(Socket sck, Func<Client, byte[], int, Task> callback, Action<Client> isDisconnected)
        {
            Socket = sck;
            Callback = callback;
            _lastSendTime = DateTime.Now; //init before check timeout
            _isDisconnected = isDisconnected;
            CheckTimeout();
        }
        bool SocketConnected(Socket s)
        {
            try
            {
                bool part1 = s.Poll(1000, SelectMode.SelectRead);
                bool part2 = (s.Available == 0);
                if (part1 && part2)
                    return false;
                else
                    return true;
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
        private void CheckTimeout()
        {
            Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    //Console.WriteLine("Call time");
                    var idlerTime = DateTime.Now - _lastSendTime;
                    if (idlerTime > _timeout)
                    {
                        IPEndPoint endPoint = this.Socket.RemoteEndPoint as IPEndPoint;
                        Console.WriteLine($"Client {endPoint.Address} has been idle for too long, disconnecting...");
                        Dispose();
                        break;
                    }
                    if (!SocketConnected(this.Socket))
                    {
                        Console.WriteLine("Not connected anymore, this socket will be close...");
                        Dispose();
                        break;
                    }
                    await Task.Delay(10000, _cts.Token);
                }
            });
        }

        public Task<int> ReceiveAsync(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            var tcs = new TaskCompletionSource<int>();
            Socket.BeginReceive(buffer, offset, size, socketFlags, ar => {
                try
                {
                    tcs.TrySetResult(Socket.EndReceive(ar));
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }, state: null);
            return tcs.Task;
        }
        public async Task ProcessDataHandler(byte[] buffer, int byteRead)
        {
            if (Callback != null)
            {
                await Callback(this, buffer, byteRead);
            }
            //Callback?.Invoke(this, buffer, byteRead);
        }
        public async Task StartReceiving(int bufferSize = 1025)
        {
            try
            {
                while (true)
                {
                    byte[] buffer = new byte[bufferSize];
                    //always wait until received 1025 bytes dùng cho fixed length
                    int totalRead = 0;
                    while (totalRead < bufferSize)
                    {
                        int byteRead = await ReceiveAsync(buffer, totalRead, bufferSize - totalRead, SocketFlags.None);
                        if (byteRead == 0)
                        {
                            Console.WriteLine("No data received, client disconnected");
                            break;
                        }
                        totalRead += byteRead;
                    }
                    _lastSendTime = DateTime.Now;
                    //copy data before, AI recommend
                    var dataCopy = new byte[totalRead];
                    Array.Copy(buffer, 0, dataCopy, 0, totalRead);
                    IPEndPoint ep = Socket.RemoteEndPoint as IPEndPoint;
                    Console.WriteLine($"Received {totalRead} from {ep.Address.ToString()}");
                    try
                    {
                        //Console.WriteLine($"Received {dataCopy.Length} bytes from {this.Socket.RemoteEndPoint.AddressFamily.ToString()}");
                        await ProcessDataHandler(dataCopy, dataCopy.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error when received data from {this.Socket.RemoteEndPoint.AddressFamily.ToString()} - {ex.Message}");
                    }
                    /**int byteRead = await ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None);

                    if (byteRead == 0)
                    {
                        Console.WriteLine("Client Disconnected");
                        break;
                    }
                    _lastSendTime = DateTime.Now;
                    var messageBuffer = new byte[byteRead];
                    Array.Copy(buffer, 0, messageBuffer, 0, byteRead);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Console.WriteLine($"Received {byteRead} bytes from {this.Socket.RemoteEndPoint.AddressFamily.ToString()}");
                            await ProcessDataHandler(messageBuffer, byteRead);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error when received data from {this.Socket.RemoteEndPoint.AddressFamily.ToString()} - {ex.Message}");
                        }
                    });**/
                }
            }
            catch
            {

            }
            finally
            {
                try { Socket?.Shutdown(SocketShutdown.Both); } catch { }
                Socket?.Close();
                Socket?.Dispose();
            }
        }
        private bool isDisposed = false;

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                Socket?.Shutdown(SocketShutdown.Both);
            }
            catch { }

            Callback = null;
            Socket?.Close();
            Socket?.Dispose();
            _isDisconnected.Invoke(this);
        }
    }
}
