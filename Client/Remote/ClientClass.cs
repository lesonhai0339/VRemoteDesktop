using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class ClientClass
    {
        private Timer _timer;
        private SocketRemoteClient _remoteClient;
        private ConnectionInfo _connectionInfo;
        public ClientClass(SocketRemoteClient remoteCLient, ConnectionInfo info)
        {
            Client = remoteCLient;
            _connectionInfo = info;
            _timer = new Timer(SendScreen, null, 0, 1000);
        }
        #region Properties
        public SocketRemoteClient Client
        {
            get=> _remoteClient;
            set
            {
                if(_remoteClient != null)
                {
                }
                _remoteClient = value;
                if(_remoteClient != null)
                {
                }
            }
        }
        #endregion
        #region Functions
        private void SendScreen(object state)
        {
            var x = CaptureScreen.GetScreen();
            if (x.Any())
            {
                int totalSize = x.Sum(cell => cell.TotalSize);
                byte[] data = new byte[5];
                data[0] = 1;
                Array.Copy(BitConverter.GetBytes(totalSize), 0, data, 0, 4);
                Send(Enums.DataType.P2PDATASEND, data);
                return;
                foreach (var cell in x)
                {
                    SendChunk(cell);
                }
            }
        }
        private void SendChunk(CaptureCell cell)
        {
            //only send 1024 byte each packet and 1 byte using for type then the real data can send is 1023 bytes
            int headers = 22; //header for type(1) + x(4) + y(4) + width(4) + height(4) + index(1) + chunkSize(4)
            int dataSize = 1023 - headers; //data 
            int chunkSize = dataSize + headers;

            //int x = cell.Bytes.Length % 1023;
            //int numberOfChunkx = (cell.Bytes.Length / 1023) + (cell.Bytes.Length % 1023 > 0 ? 1 : 0);
            int numberOfChunk = (int)Math.Ceiling((double)cell.Bytes.Length / dataSize);

            byte[] xBytes = BitConverter.GetBytes(cell.Rectangle.X);
            byte[] yBytes = BitConverter.GetBytes(cell.Rectangle.Y);
            byte[] widthBytes = BitConverter.GetBytes(cell.Rectangle.Width);
            byte[] heightBytes = BitConverter.GetBytes(cell.Rectangle.Height);

            for (int i =0; i< numberOfChunk; i++)
            {
                byte[] packet = new byte[chunkSize];

                //headers
                packet[0] = 40; //type, chunksend
                Array.Copy(xBytes, 0, packet, 1, 4);      // X: bytes[1-4]
                Array.Copy(yBytes, 0, packet, 5, 4);      // Y: bytes[5-8]
                Array.Copy(widthBytes, 0, packet, 9, 4);  // Width: bytes[9-12]
                Array.Copy(heightBytes, 0, packet, 13, 4); // Height: bytes[13-16]
                packet[17] = (byte)i;//chunk index

                int sourceOffset = i * dataSize;
                int copyLength = Math.Min(dataSize, cell.Bytes.Length - sourceOffset);

                //chunkSize
                byte[] dataLengthBytes = BitConverter.GetBytes(copyLength);
                Array.Copy(dataLengthBytes, 0, packet, 18, 4);

                //data
                Array.Copy(cell.Bytes, sourceOffset, packet, headers, copyLength);
                Console.WriteLine($"X: {packet[0]} - {packet[1]}");
                Send(Enums.DataType.P2PDATASEND, packet);
            }

        }
        private void Send(Enums.DataType type, byte[] data, int timeout = 5)
        {
            _remoteClient.Send(type, data);
        }
        #region Events
        #endregion
        #endregion
    }
}
