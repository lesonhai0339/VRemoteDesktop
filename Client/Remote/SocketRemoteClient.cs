using Newtonsoft.Json;
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
        private bool isSendScreen;
        private byte[] bytesBuilder;


        public delegate void ConnectedEvent();
        public event ConnectedEvent ConnectedEventHandler;
        public delegate void LoginEvent();
        public event LoginEvent LoginEventHandler;

        public delegate void P2PConnectEvent(bool isRemote,ConnectionInfo info);
        public event P2PConnectEvent P2PConnectEventHandler;

        public delegate void P2PDataSendSuccessEvent();
        public event P2PDataSendSuccessEvent P2PDataSendSuccessEventHandler;
        public delegate void SendScreenEvent(byte[] data);
        public event SendScreenEvent SendScreenEventHandler;
        public SocketRemoteClient() 
        {
            isSendScreen = false;
            bytesBuilder = new byte[0];
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

                    if (!isSendScreen)
                    {
                        ProcessDataReceived(stateObject, dataBytes);
                    }
                    else
                    {
                        ProcessDataScreen(dataBytes);
                    }
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

        private void ProcessDataScreen(byte[] dataBytes)
        {
            byte[] newBytes = new byte[bytesBuilder.Length + dataBytes.Length];
            bytesBuilder.CopyTo(newBytes, 0);
            dataBytes.CopyTo(newBytes, bytesBuilder.Length);
            bytesBuilder = newBytes;
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
                case 10:
                    Console.WriteLine("P2P connected Remote");
                    ProcessP2PConnection(data, true);
                    break;
                case 11:
                    Console.WriteLine("P2P connected Client");
                    ProcessP2PConnection(data, false);
                    break;
                case 20:
                    Console.WriteLine("P2P Data received");
                    break;
                case 30:
                    //p2p data send success
                    P2PDataSendSuccessEvent p2PDataSendSuccess = P2PDataSendSuccessEventHandler;
                    if(p2PDataSendSuccess != null)
                    {
                        p2PDataSendSuccess();
                    }
                    break;
                case 40:
                    //p2p send screen
                    isSendScreen = true;
                    break;
                case 41:
                    //p2p send finished
                    isSendScreen = false;
                    SendScreenEvent sendScreenEvent = SendScreenEventHandler;
                    if(sendScreenEvent != null)
                    {
                        sendScreenEvent(bytesBuilder);
                    }
                    bytesBuilder = new byte[0];
                    break;
                case 90:
                    Console.WriteLine("P2P connect error");
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
        private void ProcessP2PConnection(byte[] data, bool isRemote)
        {
            byte[] bytesData = new byte[data.Length - 1];
            Array.Copy(data, 1, bytesData, 0, bytesData.Length);
            string[] dataStrings = Encoding.UTF8.GetString(bytesData).Split('|');
            if(dataStrings.Length != 8)
            {
                throw new Exception("Missing some value");
            }
            ConnectionInfo connectionInfo = new ConnectionInfo(
                sessionId: dataStrings[0],
                partner: new Info
                {
                    Id = dataStrings[1],
                    Password = dataStrings[2],
                    ComputerName = dataStrings[3],
                    Width = int.Parse(dataStrings[4]),
                    Height = int.Parse(dataStrings[5]),
                    MajorVersion = dataStrings[6],
                    MinorVersion = dataStrings[7],
                });
            if (isRemote)
            {
                P2PConnectEvent p2pConnectEvent = P2PConnectEventHandler;
                if (p2pConnectEvent != null)
                {
                    p2pConnectEvent(true, connectionInfo);
                }
            }
            else
            {
                P2PConnectEvent p2pConnectEvent = P2PConnectEventHandler;
                if (p2pConnectEvent != null)
                {
                    p2pConnectEvent(false,connectionInfo);
                }
            }
            Console.WriteLine($"Client Info: {JsonConvert.SerializeObject(connectionInfo, Formatting.Indented)}");
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
