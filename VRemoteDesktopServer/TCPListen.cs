using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static VRemoteDesktopServer.Enums;

namespace VRemoteDesktopServer
{
    internal class TCPListen
    {
        private Queue<QueueData> _queues;
        private Socket _listener;
        private BackgroundWorker _worker;
        private ManualResetEvent _manualResetEvent;
        private ConnectionsManager _connectionsManager;
        public TCPListen()
        {
            _queues = new Queue<QueueData>();
            _connectionsManager = new ConnectionsManager();
            Worker = new BackgroundWorker();
            _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _manualResetEvent = new ManualResetEvent(false);

        }
        #region Attributes
        internal virtual BackgroundWorker Worker
        {
            get => _worker;
            set 
            {
                DoWorkEventHandler handler = new DoWorkEventHandler(QueueEventHandler);
                BackgroundWorker backgroundWorker = _worker;
                if(backgroundWorker != null)
                {
                    backgroundWorker.DoWork -= handler;
                }
                _worker = value;
                backgroundWorker = _worker;
                if(backgroundWorker != null)
                {
                    backgroundWorker.DoWork += handler;
                }
            }
        }
        #endregion
        #region Functions
        public void QueueEventHandler(object sender, DoWorkEventArgs e)
        {
            Console.WriteLine("Worker started");
            // Simulate some work
            while (true)
            {
                if (_queues.Count > 0)
                {
                    Console.WriteLine($"Queue count {_queues.Count}");
                    var tempQueue = _queues.Peek();
                    if (!_connectionsManager.IsExisted(tempQueue.Id))
                    {
                        Console.WriteLine("Session is Null");
                        Thread.Sleep(10);
                        continue;
                    }

                    Socket sck = (tempQueue.Type == RemoteType.OWNER)
                        ? _connectionsManager.Get(tempQueue.Id).Owner
                        : _connectionsManager.Get(tempQueue.Id).Partner;
                    if (sck == null)
                    {
                        Console.WriteLine("Remote Socket is Null");
                        Thread.Sleep(10);
                        continue;
                    }
                    else
                    {
                        var queue = _queues.Dequeue();
                        sck.Send(queue.ByteData);
                    }

                }
                Console.WriteLine("Worked");
                Thread.Sleep(10);
            }
        }
        public void Listen(int port)
        {
            if (!Worker.IsBusy)
            {
                Worker.RunWorkerAsync();
            }
            int portListen = (port == 0) ? 2399 : port;
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, portListen);
            _listener.Bind(ep);
            _listener.Listen(10);
            try
            {
                _listener.BeginAccept(new AsyncCallback(AcceptCallback), _listener);
            }
            catch (SocketException ex)
            {
                Console.WriteLine("SocketException: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
            finally
            {
                _listener.Close();
            }
        }
        private void AcceptCallback(IAsyncResult asyncResult)
        {
            try
            {
                Socket workSocket = _listener.EndAccept(asyncResult);
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = workSocket;

                workSocket.BeginReceive(stateObject.Buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReceivedCallback), stateObject);
                
                _listener.BeginAccept(new AsyncCallback(AcceptCallback), null);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"AcceptCallback Error: {ex.Message}");
            }
        }
        public void ReceivedCallback(IAsyncResult asyncResult)
        {
            try
            {
                StateObject stateObject = (StateObject)asyncResult.AsyncState;
                Socket workSocket = stateObject.WorkSocket;

                int num = workSocket.EndReceive(asyncResult);
                if (num > 0 )
                {
                    byte[] dataReceived = new byte[num];
                    Array.Copy(stateObject.Buffer, dataReceived, num);

                    ProcessDataEventHandler(workSocket,dataReceived);

                    workSocket.BeginReceive(stateObject.Buffer, 0, 1024, SocketFlags.None, new AsyncCallback(ReceivedCallback), stateObject);
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"ReceivedCallback Error: {ex.Message}");
            }
        }
        private void ProcessDataEventHandler(Socket sck,byte[] receivedData)
        {
            SendType dataType = (SendType)receivedData[0];
            RemoteType remoteType = (RemoteType)receivedData[1];
            string remoteId = Encoding.ASCII.GetString(receivedData.Skip(2).Take(8).ToArray());
            byte[] data = receivedData.Skip(10).ToArray();
            switch (dataType)
            {
                case SendType.INIT_CONNECTION:
                    // Init remote connection and stored in the dictionary
                    if (remoteType == RemoteType.OWNER)
                    {
                        _connectionsManager.Add(remoteId, sck, null);
                    }
                    else
                    {
                        _connectionsManager.Add(remoteId, null, sck);
                    }
                    break;
                case SendType.PING:
                    // Update ping for the connection
                    _connectionsManager.UpdatePing(remoteId, remoteType);
                    // Handle data type 1
                    break;
                case SendType.SHARESCREEN:
                    _queues.Enqueue(new QueueData(
                        id: remoteId,
                        type: remoteType,
                        byteData: data
                    ));
                    // Handle data type 2
                    break;
                case SendType.SENDKEY:
                    // Handle data type 3
                    break;
                case SendType.SENDMOUSE:
                    // Handle data type 4
                    break;
                case SendType.SENDTEXT:
                    // Handle data type 5
                    break;
                case SendType.SENDSHORTCUT:
                    // Handle data type 6
                    break;
                case SendType.SENDFILE:
                    // Handle data type 7
                    break;
                default:
                    Console.WriteLine($"Unknown data type: {dataType}");
                    break;
            }
        }
        #endregion

    }
}
