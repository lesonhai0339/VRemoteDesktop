using System;
using System.Collections.Generic;
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
            _timer = new Timer(SendScreen, null, 0, 10000);
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
            Console.WriteLine("SendScreen called at " + DateTime.Now.ToString("HH:mm:ss.fff"));
            byte[] bytes;

            using (Bitmap capture = CaptureScreen.CaptureWindowsScreen())
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    capture.Save(stream, ImageFormat.Png);
                    bytes = stream.ToArray();
                }
            }
            int totalBytes = bytes.Length;
            SendScreenData1(totalBytes, bytes);
            //Console.WriteLine("SendScreen called at " + DateTime.Now.ToString("HH:mm:ss.fff"));
            //var x = CaptureScreen.GetScreen();
            //if (x.Any())
            //{
            //    int totalBytes = x.Sum(cell => cell.Bytes.Length);
            //    Console.WriteLine("DataSend: " + totalBytes);
            //    foreach (var cell in x)
            //    {
            //        SendScreenData(totalBytes ,cell);
            //    }
            //}
        }
        private void SendScreenData1(int totalBytes, byte[] byteData)
        {
            int CHUNK_SIZE = 1024;

            byte[] bytes = new byte[byteData.Length + 9];

            Array.Copy(BitConverter.GetBytes(totalBytes), 0, bytes, 0, 4); // Add total bytes at the start

            //caculate padding need to add
            int lastChunkSize = bytes.Length % CHUNK_SIZE;
            int padding = 0;
            if(lastChunkSize != 0)
            {
                padding = CHUNK_SIZE - lastChunkSize;
            }
            Array.Copy(BitConverter.GetBytes(padding), 0 , bytes, 4, 4);

            bytes[8] = 4; // Type of data, 4 for screen data

            //data
            Array.Copy(byteData, 0, bytes, 9, byteData.Length);//real data

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
        private void SendScreenData(int totalBytes, CaptureCell cell)
        {
            var cellBytes = cell.Bytes;
            byte[] bytes = new byte[cellBytes.Length + 5];

            Array.Copy(BitConverter.GetBytes(totalBytes), 0, bytes, 0, 4); // Add total bytes at the start
            bytes[4] = 4; // Type of data, 4 for screen data
            Array.Copy(cellBytes, 0, bytes, 5, cellBytes.Length);//real data


            int CHUNK_SIZE = 1024;

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
            //only send 1024 byte each packet and 1 byte using for type then the real data can send is 1023 bytes
            int headers = 22; //header for type(1) + x(4) + y(4) + width(4) + height(4) + index(1) + chunkSize(4)
            int dataSize = 1024 - headers; //data 
            int chunkSize = dataSize + headers;

            //int x = cell.Bytes.Length % 1023;
            //int numberOfChunkx = (cell.Bytes.Length / 1023) + (cell.Bytes.Length % 1023 > 0 ? 1 : 0);
            int numberOfChunk = (int)Math.Ceiling((double)cell.Bytes.Length / dataSize);

            byte[] xBytes = BitConverter.GetBytes(cell.Rectangle.X);
            byte[] yBytes = BitConverter.GetBytes(cell.Rectangle.Y);
            byte[] widthBytes = BitConverter.GetBytes(cell.Rectangle.Width);
            byte[] heightBytes = BitConverter.GetBytes(cell.Rectangle.Height);


            //Console.WriteLine(BitConverter.ToString(xBytes));
            //Console.WriteLine(BitConverter.ToString(yBytes));
            //Console.WriteLine(BitConverter.ToString(widthBytes));
            //Console.WriteLine(BitConverter.ToString(heightBytes));

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
                if(((i + 1) % 5) == 0)
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
