using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
                foreach(var cell in x)
                {
                    SendChunk(cell);
                }
            }
        }
        private void SendChunk(CaptureCell cell)
        {
            int dataSize = 1024; //data 1024 
            int headers = 18; //header for type(1) + x(4) + y(4) + width(4) + height(4) + index(1)
            int chunkSize = dataSize + headers;

            //int x = cell.Bytes.Length % 1024;
            //int numberOfChunkx = (cell.Bytes.Length / 1024) + (cell.Bytes.Length % 1024 > 0 ? 1 : 0);
            int numberOfChunk = (int)Math.Ceiling((double)cell.Bytes.Length / 1024);

            byte[] xBytes = BitConverter.GetBytes(cell.Rectangle.X);
            byte[] yBytes = BitConverter.GetBytes(cell.Rectangle.Y);
            byte[] widthBytes = BitConverter.GetBytes(cell.Rectangle.Width);
            byte[] heightBytes = BitConverter.GetBytes(cell.Rectangle.Height);

            for (int i =0; i< numberOfChunk; i++)
            {
                byte[] bytes = new byte[chunkSize];

                //headers
                bytes[0] = 40; //type, chunksend
                Array.Copy(xBytes, 0, bytes, 1, 4);      // X: bytes[1-4]
                Array.Copy(yBytes, 0, bytes, 5, 4);      // Y: bytes[5-8]
                Array.Copy(widthBytes, 0, bytes, 9, 4);  // Width: bytes[9-12]
                Array.Copy(heightBytes, 0, bytes, 13, 4); // Height: bytes[13-16]
                bytes[17] = (byte)i;//chunk index

                //data
                int sourceOffset = i * dataSize;
                int copyLength = Math.Min(dataSize, cell.Bytes.Length - sourceOffset);
                Array.Copy(cell.Bytes, sourceOffset, bytes, headers, copyLength);

                Send(Enums.DataType.P2PDATASEND,  bytes);
                Console.WriteLine("Sent: "+ bytes.Length);
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
