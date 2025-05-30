using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static RemoteClient.Enums;

namespace RemoteClient
{
    public class TCPClient
    {
        private Socket _sck;
        private Chunk _chunk;
        private SocketConnection _socketConnection;
        private StateObject stateObject;

        public event EventHandler<ImageEventArgs> ImageReceived;
        public event EventHandler<TextEventArgs> TextReceived;


        public delegate void DataResponseHandler(bool flag);
        private DataResponseHandler _dataResponseEventHandler;
        public event DataResponseHandler DataResponseEvent
        {
            add { _dataResponseEventHandler += value; }
            remove { _dataResponseEventHandler -= value; }
        }
        public TCPClient()
        {
            stateObject = new StateObject();
            _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _chunk = new Chunk();
            _socketConnection = new SocketConnection(_sck, Callback);   
        }
        public void ReceivedCallback(IAsyncResult asyncResult)
        {
            try
            {
                StateObject stateObject = (StateObject)asyncResult.AsyncState;
                Socket workSocket = stateObject.WorkSocket;
                int num = workSocket.EndReceive(asyncResult);
                Console.WriteLine(num);
                if (num > 0)
                {
                    //workSocket.BeginSend(Encoding.ASCII.GetBytes("OK"), 0, StateObject.BufferSize, SocketFlags.None, ReceivedCallback, stateObject);
                    byte[] dataBytes = new byte[num];
                    Buffer.BlockCopy(stateObject.Buffer, 0, dataBytes, 0, num);

                    ProcessDataReceived(stateObject, dataBytes);
                    
                    //int a = dataBytes[0];

                    //if (a == 4)
                    //{
                    //    int totalLength = BitConverter.ToInt32(dataBytes, 1);
                    //    Console.WriteLine($"Received Chunk {totalLength}");
                    //    _chunk.Init(totalLength);
                    //    TextReceived?.Invoke(this, new TextEventArgs($"Received Chunk {totalLength}"));
                    //}
                    //else if (a == 1)
                    //{
                    //    byte[] newData = dataBytes.Skip(1).ToArray();
                    //    Console.WriteLine(BitConverter.ToString(newData));
                    //    TextReceived?.Invoke(this, new TextEventArgs($"New Data {newData.Length}"));

                    //    bool result = _chunk.Add(newData);
                    //    Console.WriteLine($"Length {_chunk.GetDataLength()}");
                    //    if (result)
                    //    {
                    //        Console.WriteLine($"Received a complete chunk of data: {_chunk.GetDataLength()}");
                    //        Console.WriteLine(BitConverter.ToString(_chunk.GetData()));
                    //        ImageReceived?.Invoke(this, new ImageEventArgs(_chunk.GetData()));
                    //    }
                    //}
                    //else
                    //{
                    //    Console.WriteLine($"Received a: {a}");
                    //}
                    //Console.WriteLine($"Received: {data}");
                    //Console.WriteLine("------------------\n");
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

            DataSendType dataType = (Enums.DataSendType)data[0];
            switch (dataType)
            {
                case DataSendType.KEYBOARD:
                    KeyState keyState = (KeyState)data[1];
                    if(keyState == KeyState.KeyDown)
                    {
                        byte[] byteSend = new byte[1024];
                        byte type = 0x02;
                        byte isHost = 0x02;

                        byte[] sessionId = Encoding.ASCII.GetBytes("11111111");

                        byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARDRECEIVED};
                        byteSend[0] = type;
                        byteSend[1] = isHost;

                        Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                        Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                        stateObject.WorkSocket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);

                    }
                    else
                    {
                        byte[] byteSend = new byte[1024];
                        byte type = 0x02;
                        byte isHost = 0x02;

                        byte[] sessionId = Encoding.ASCII.GetBytes("11111111");

                        byte[] byteData = new byte[] { (byte)DataSendType.KEYBOARDRECEIVED };
                        byteSend[0] = type;
                        byteSend[1] = isHost;

                        Array.Copy(sessionId, 0, byteSend, 2, sessionId.Length);
                        Array.Copy(byteData, 0, byteSend, 10, byteData.Length);
                        stateObject.WorkSocket.BeginSend(data, 0, data.Length, SocketFlags.None, null, null);
                    }
                    break;
                case DataSendType.KEYBOARDRECEIVED:
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
            buffer[0] = 1;
            buffer[1] = 1;
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
                _sck.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(ReceivedCallback), stateObject);
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
