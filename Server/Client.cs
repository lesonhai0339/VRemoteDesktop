using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
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
        public void ProcessDataHandler(byte[] buffer, int byteRead)
        {
            Callback?.Invoke(this, buffer, byteRead);
        }
        public async Task StartReceiving()
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int byteRead = await ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None);
                    if (byteRead == 0)
                    {
                        Console.WriteLine("Client Disconnected");
                        break;
                    }
                    ProcessDataHandler(buffer, byteRead);
                }
            }
            catch
            {

            }
            finally
            {
                Socket.Close();
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
