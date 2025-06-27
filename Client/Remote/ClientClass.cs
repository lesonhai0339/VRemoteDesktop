using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
            _timer = new Timer(SendScreen, null, 0, (1000/10));
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
        #region Methods
        private void SendScreen(object state)
        {
            var x = CaptureScreen.GetScreen();
            if (x.Any())
            {
                foreach (var cell in x)
                {
                    if (cell.IsFullScreen)
                    {
                        SendScreenData(cell);
                    }
                    else
                    {
                        SendChunk(cell);
                        Console.WriteLine($"{cell.Rectangle.X} - {cell.Rectangle.Y} - {cell.Rectangle.Width} - {cell.Rectangle.Height}");
                    }
                }
            }
        }
        private void SendScreenData(CaptureCell cell)
        {
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[cell.Bytes.Length + 9];

            Array.Copy(BitConverter.GetBytes(cell.Bytes.Length + 1), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);

            bytes[8] = 4;

            //data
            Array.Copy(cell.Bytes, 0, bytes, 9, cell.Bytes.Length);//real data

            int numberOfChunk = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Array.Copy(bytes, offset, packet, 0, packetSize);
                if (((i + 1) % 5) == 0)
                {
                    Thread.Sleep(1);
                }
                Client.SendData(Enums.DataType.P2PDATASEND, packet);
            }
        }
        private void SendChunk(CaptureCell cell)
        {
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[cell.Bytes.Length + 25];

            Array.Copy(BitConverter.GetBytes(cell.Bytes.Length + 17), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if (lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0, bytes, 4, 4);

            bytes[8] = 5; //send chunk


            int x = cell.Rectangle.X;
            int y = cell.Rectangle.Y;
            int width = cell.Rectangle.Width;
            int height = cell.Rectangle.Height;
            Array.Copy(BitConverter.GetBytes(x), 0, bytes, 9, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(y), 0, bytes, 13, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(width), 0, bytes, 17, 4);//4 bytes
            Array.Copy(BitConverter.GetBytes(height), 0, bytes, 21, 4);//4 bytes

            //remaining data
            Array.Copy(cell.Bytes, 0, bytes, 25, cell.Bytes.Length);//real data

            int numberOfChunk = (int)Math.Ceiling((double)bytes.Length / CHUNK_SIZE);

            for (int i = 0; i < numberOfChunk; i++)
            {
                int offset = i * CHUNK_SIZE;
                int packetSize = Math.Min(CHUNK_SIZE, bytes.Length - i * CHUNK_SIZE);
                byte[] packet = new byte[packetSize];

                //data
                Array.Copy(bytes, offset, packet, 0, packetSize);
                if (((i + 1) % 5) == 0)
                {
                    Thread.Sleep(1);
                }
                Client.SendData(Enums.DataType.P2PDATASEND, packet);
            }
        }
        #region Events
        #endregion
        #endregion
    }
}
