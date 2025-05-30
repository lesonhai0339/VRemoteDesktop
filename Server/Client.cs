using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Client
    {
        public Client(Socket sck, Func<Client, byte[],int, Task> callback)
        {
            Socket = sck;
            Callback = callback;
        }
        public Socket Socket { get; private set; }
        public Func<Client, byte[], int, Task> Callback { get; set; }
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
        public async Task StartReceiving()
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int byteRead = await ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None);
                    var messageBuffer = new byte[byteRead];
                    Array.Copy(buffer, 0, messageBuffer, 0, byteRead);
                    if (byteRead == 0)
                    {
                        Console.WriteLine("Client Disconnected");
                        break;
                    }
                    _ = Task.Run( async() =>
                    {
                        try
                        {
                            await ProcessDataHandler(messageBuffer, byteRead);
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine($"Error when received data from {this.Socket.RemoteEndPoint.AddressFamily.ToString()} - {ex.Message}");
                        }
                    });
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
        }
    }
}
