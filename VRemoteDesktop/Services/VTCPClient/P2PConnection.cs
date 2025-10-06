using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class P2PConnection : IDisposable
    {
        private int _disposed;
        private bool connected = false;
        private Timer _timer;
        private Socket p2pSocket;
        private ManualResetEvent _resetEvent;
        private CancellationTokenSource _cancel = new CancellationTokenSource();
        public event EventHandler<EventArgs> P2PEvent;
        public P2PConnection()
        {
            _resetEvent = new ManualResetEvent(false);
            p2pSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            p2pSocket.NoDelay = true;
            p2pSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            _timer = new Timer(CheckTimeout, null, 5000, Timeout.Infinite);
        }

        private void CheckTimeout(object state)
        {
            if (!connected)
            {
                //False
                Console.WriteLine("Failed");
            }
        }

        public void Listen()
        {
            EndPoint endpoint = new IPEndPoint(IPAddress.Any, 2399);
            p2pSocket.Bind(endpoint);
            p2pSocket.Listen(1);
            p2pSocket.BeginAccept(AcceptCallback, p2pSocket);
            _resetEvent.WaitOne();  
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            _resetEvent.Set();
            Console.WriteLine("Succeeded");
            connected = !connected;
            var sck = ar.AsyncState as Socket;
            var client = sck.EndAccept(ar);

            //end listen
            sck.Close();

            StateObject stateObject = new StateObject();
            stateObject.WorkSocket = client;

            client.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);

        }

        private void DataCallback(IAsyncResult ar)
        {
            StateObject stateObject = (StateObject)ar.AsyncState;
            Socket workSocket = stateObject.WorkSocket;
            int num = workSocket.EndReceive(ar);
            if(num > 0)
            {
                stateObject.ByteArrayBuilder.Append(stateObject.Buffer, 0, num);          
            }
            try
            {
                workSocket.BeginReceive(stateObject.Buffer, 0, stateObject.BufferSize, SocketFlags.None, new AsyncCallback(DataCallback), stateObject);
            }
            catch
            {
                workSocket.Close();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
            }
        }   
    }
}
