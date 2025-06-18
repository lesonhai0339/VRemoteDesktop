using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class SocketRemoteClient: IDisposable
    {
        private bool _isSocketConnected;
        private Socket _socket;


        public delegate void ConnectedEvent();
        public event ConnectedEvent ConnectedEventHandler;
        public delegate void LoginEvent();
        public event LoginEvent LoginEventHandler;
        public SocketRemoteClient() 
        {
        }
        #region Properties
        public Socket Socket
        {
            get => _socket;
            private set
            {
                _socket = value;
            }
        }
        public bool SocketConnected
        {
            get => _isSocketConnected;
            private set
            {
                _isSocketConnected = value;
            }
        }
        #endregion
        #region Functions
        public void Connect(IPEndPoint endPoint)
        {
            try
            {
                if (Socket == null)
                {
                    Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
                Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                Socket.BeginConnect(endPoint, new AsyncCallback(ConnectCallback), Socket);
            }
            catch(SocketException ex)
            {
                Console.WriteLine(string.Format("Connect SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Connect Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
            finally
            {
                //Socket.Close();
            }
        }
        public void Send(Enums.DataType type, byte[] data)
        {
            try
            {
                byte[] dataSend = new byte[1 + data.Length];
                dataSend[0] = (byte)type;
                Array.Copy(data, 0, dataSend, 1, data.Length);
                Socket.BeginSend(dataSend, 0, dataSend.Length, SocketFlags.None, null, null);
            }
            catch(SocketException ex)
            {
                Console.WriteLine(string.Format("Socket Send error: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }
        private void ConnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                Socket.EndConnect(asyncResult);
                Console.WriteLine("Client connected: " + Socket.RemoteEndPoint);
                if (Socket.Connected)
                {
                    SocketConnected = true;
                }
                ConnectedEvent connectedEvent = ConnectedEventHandler;
                if(connectedEvent != null)
                {
                    connectedEvent();
                }
                StateObject stateObject = new StateObject();
                stateObject.WorkSocket = Socket;


                Socket.BeginReceive(stateObject.Buffer, 0, 1024, SocketFlags.None, new AsyncCallback(Callback), stateObject);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(string.Format("ConnectCallback SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch(Exception ex)
            {
                Console.WriteLine(string.Format("ConnectCallback Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }
        private void Callback(IAsyncResult asyncResult)
        {
            try
            {
                StateObject stateObject = (StateObject)asyncResult.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = Socket.EndReceive(asyncResult);
                if (num > 0)
                {
                    byte[] dataBytes = new byte[num];
                    Buffer.BlockCopy(stateObject.Buffer, 0, dataBytes, 0, num);

                    ProcessDataReceived(stateObject, dataBytes);
                }
                workSocket.BeginReceive(stateObject.Buffer, 0, StateObject.BufferSize, SocketFlags.None, Callback, stateObject);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(string.Format("Callback SocketException: {0} - {1}", ex.Message, ex.StackTrace));
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Callback Exception: {0} - {1}", ex.Message, ex.StackTrace));
            }
        }
        private void ProcessDataReceived(StateObject stateObject, byte[] data)
        {
            Console.WriteLine("Callback Received");
            int response = data[0];
            switch (response)
            {
                case 1:
                    Console.WriteLine("Ping successfully");
                    break;
                case 2:
                    Console.WriteLine("Login successfully");
                    LoginEvent loginEvent = LoginEventHandler;
                    if(loginEvent != null)
                    {
                        loginEvent();
                    }
                    break;
                case 97:
                case 98:
                case 99:
                    Console.WriteLine("Error");
                    break;
                default:
                    break;
            }
        }

        public void Dispose()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                _socket?.Dispose();
            }
            catch { }
            finally
            {
                _socket = null;
                SocketConnected = false;
            }
        }
        #endregion
    }
}
