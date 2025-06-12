using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RemoteClient.Enums;

namespace RemoteClient
{
    public class TCPClient
    {
        private RemoteType _remoteType;
        public Socket _sck;
        private Chunk _chunk;
        private SocketConnection _socketConnection;
        private StateObject stateObject;

        public event EventHandler<ImageEventArgs> ImageReceived;
        public event EventHandler<TextEventArgs> TextReceived;

        private KeyboardSimulator _keyboardSimulator;
        public delegate void DataResponseHandler(bool flag);
        private DataResponseHandler _dataResponseEventHandler;
        public event DataResponseHandler DataResponseEvent
        {
            add { _dataResponseEventHandler += value; }
            remove { _dataResponseEventHandler -= value; }
        }
        public TCPClient(RemoteType remoteType)
        {
            _remoteType = remoteType;
            stateObject = new StateObject();
            _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _chunk = new Chunk();
            _socketConnection = new SocketConnection(_sck, Callback);   
            _keyboardSimulator = new KeyboardSimulator();
        }
        public void ReceivedCallback(IAsyncResult asyncResult)
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
                workSocket.BeginReceive(stateObject.Buffer, 0, StateObject.BufferSize, SocketFlags.None, ReceivedCallback, stateObject);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data received Error: {ex.Message}");
            }
        }
        private void ProcessDataReceived(StateObject stateObject, byte[] data)
        {
            DataSendType dataType = (DataSendType)data[0];
            switch (dataType)
            {
                case DataSendType.KEYBOARD:
                    KeyState keyState = (KeyState)data[1];
                    Keys key = (Keys)data[2];
                    if(keyState == KeyState.KeyDown)
                    {
                        uint status = _keyboardSimulator.SendKey(key);
                        if(status != 0)
                        {
                            Console.WriteLine("Send");
                            byte[] byteSend = new byte[1024];
                            byte type = (int)PackageType.DATA;
                            byte remoteType = (byte)(int)_remoteType;

                            byte[] sessionId = Encoding.ASCII.GetBytes("11111111");

                            byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARDRECEIVED };
                            byteSend[0] = type;
                            byteSend[1] = remoteType;

                            Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                            Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                            stateObject.WorkSocket.BeginSend(byteSend, 0, byteSend.Length, SocketFlags.None, null, null);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Send1");

                        byte[] byteSend = new byte[1024];
                        byte type = (int)PackageType.DATA;
                        byte remoteType = (byte)(int)_remoteType;

                        byte[] sessionId = Encoding.ASCII.GetBytes("11111111");

                        byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARDRECEIVED };
                        byteSend[0] = type;
                        byteSend[1] = remoteType;

                        Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                        Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                        stateObject.WorkSocket.BeginSend(byteSend, 0, byteSend.Length, SocketFlags.None, null, null);
                    }
                    break;
                case DataSendType.KEYBOARDRECEIVED:
                    Console.WriteLine("KeyboardReceived");
                    DataResponseHandler handler = _dataResponseEventHandler;
                    if (handler != null)
                    {
                        handler(true);
                    }
                    break;
                case DataSendType.SCREEN:
                    break;
                case DataSendType.SCREENCHANGE:
                    break;
                case DataSendType.MOUSE:
                    break;
                default:
                    break;
            }
        }
        public byte[] InitRemote()
        {
            byte[] buffer = new byte[1024];
            buffer[0] = (int)PackageType.CONNCECT;
            buffer[1] = (byte)(int)_remoteType;
            string sessionId = "11111111";
            byte[] sessionIdBytes = Encoding.ASCII.GetBytes(sessionId);
            Buffer.BlockCopy(sessionIdBytes, 0, buffer, 2, sessionIdBytes.Length);
            return buffer;
        }
        public void Connect(IPEndPoint remoteEP)
        {
            _socketConnection.Connect(remoteEP);
        }
        public void Send(byte[] data)
        {

            if (_sck.Connected)
            {
                //không dùng callback ở đây
                _sck.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
            }
            else
            {
                Console.WriteLine("Socket is not connected.");
            }
        }
        public void Callback(IAsyncResult asyncResult)
        {
            try
            {
                _sck.EndConnect(asyncResult);
                Console.WriteLine("Client connected: " + _sck.RemoteEndPoint);

                byte[] data = InitRemote();

                _sck.Send(data);

                //StateObject stateObject = new StateObject();
                stateObject.WorkSocket = _sck;


                _sck.BeginReceive(stateObject.Buffer, 0, 1024, SocketFlags.None, new AsyncCallback(ReceivedCallback), stateObject);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data received Error: ", ex.Message);
            }
        }
    }
}
