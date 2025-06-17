using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class SocketRemoteClient
    {
        private bool isSocketConnected;
        private Socket _socket;


        public delegate void ConnectedEven();
        public event ConnectedEven ConnectedEvenHandler;
        public SocketRemoteClient() { }
        #region Properties
        public Socket Socket
        {
            get => _socket;
            private set
            {
                _socket = value;
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
                Socket.BeginConnect(endPoint, new AsyncCallback(Callback), Socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data received Error: ", ex.Message);
            }
            finally
            {
                //_sck.Close();
            }
        }
        private void Callback(IAsyncResult asyncResult)
        {
            try
            {
                StateObject stateObject = (StateObject)asyncResult.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = workSocket.EndReceive(asyncResult);
                if (num > 0)
                {
                    //workSocket.BeginSend(Encoding.ASCII.GetBytes("OK"), 0, StateObject.BufferSize, SocketFlags.None, ReceivedCallback, stateObject);
                    byte[] dataBytes = new byte[num];
                    Buffer.BlockCopy(stateObject.Buffer, 0, dataBytes, 0, num);

                    ProcessDataReceived(stateObject, dataBytes);
                }
                workSocket.BeginReceive(stateObject.Buffer, 0, StateObject.BufferSize, SocketFlags.None, Callback, stateObject);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data received Error: {ex.Message}");
            }
        }
        private void ProcessDataReceived(StateObject stateObject, byte[] data)
        {

        }
        #endregion
    }
}
