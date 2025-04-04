using System;
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
        private Socket _listener;
        private BackgroundWorker _worker;
        private ManualResetEvent _manualResetEvent;
        public TCPListen()
        {
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
            int dataType = receivedData[0];
            RemoteType remoteType = (RemoteType)receivedData[1];
            string remoteId = Encoding.ASCII.GetString(receivedData.Skip(2).Take(8).ToArray());
            byte[] data = receivedData.Skip(10).ToArray();
            switch (dataType)
            {
                case 0:
                    // Init remote connection and stored in the dictionary

                    break;
                case 1:
                    // Handle data type 1
                    break;
                default:
                    Console.WriteLine($"Unknown data type: {dataType}");
                    break;
            }
        }
        #endregion

    }
}
