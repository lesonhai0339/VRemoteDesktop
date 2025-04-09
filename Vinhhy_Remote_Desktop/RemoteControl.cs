using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Vinhhy_Remote_Desktop
{
    public class RemoteControl
    {
        private Socket _sck;
        private string _serverIp;
        private int _serverPort;
        private string _clientIp;
        private int _clientPort;
        public RemoteControl()
        {
            InitializeSocket();
        }
        #region Attributes
        public Socket Sck
        {
            get { return _sck; }
            set { _sck = value; }
        }
        public string ServerIp
        {
            get { return _serverIp; }
            set { _serverIp = value; }
        }
        public int ServerPort
        {
            get { return _serverPort; }
            set { _serverPort = value; }
        }
        public string ClientIp
        {
            get { return _clientIp; }
            set { _clientIp = value; }
        }
        public int ClientPort
        {
            get { return _clientPort; }
            set { _clientPort = value; }
        }
        #endregion
        #region Functions
        public void InitializeSocket()
        {
            Sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

            ServerIp = "27.0.12.78";
            ServerPort = 2399;
            ClientIp = "";
            ClientPort = 0;

        }
        public async Task ConnectToServer()
        {
            var address = IPAddress.Parse(ServerIp);

            IPEndPoint ep = new IPEndPoint(address, ServerPort);
            await Sck.ConnectAsync(ep);
            if (Sck.Connected)
            {
                _= Task.Run(async () => await ReceiveCallback(Sck));
                Console.WriteLine("Connected to server");
                byte[] initRoomMetadata = GenerateRemoteRoom();
                await Sck.SendAsync(new ArraySegment<byte>(initRoomMetadata), SocketFlags.None);


                //test send desktop data
                try
                {
                    byte[] desktopData = GrabDeskTop();
                    byte[] buffer = new byte[14];
                    buffer[0] = (byte)DataSendType.SCREEN;
                    buffer[1] = (byte)ConnectType.PARTNER;

                    byte[] Id = Encoding.ASCII.GetBytes("11111111");
                    Buffer.BlockCopy(Id, 0, buffer, 2, Id.Length);

                    byte[] desktopLength = BitConverter.GetBytes(desktopData.Length);

                    Buffer.BlockCopy(desktopLength, 0, buffer, 10, desktopLength.Length);

                    int result = await Sck.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    if(result == 0)
                    {
                        Console.WriteLine("Send failed");
                    }

                    for (int i = 0; i < desktopData.Length; i += 1014)
                    {
                        byte[] chunk = new byte[1024];

                        chunk[0] = (byte)DataSendType.SCREEN;
                        chunk[1] = (byte)ConnectType.PARTNER;

                        Buffer.BlockCopy(Id, 0, chunk, 2, Id.Length);

                        int chunkSize = Math.Min(1014, desktopData.Length - i);
                        Buffer.BlockCopy(desktopData, i, chunk, 10, chunkSize);
                        Console.WriteLine($"Desktop Length: {desktopData.Length}");
                        Console.WriteLine($"Chunk Size: {chunkSize}");
                        Console.WriteLine($"Index {i}");
                        int rs = await Sck.SendAsync(new ArraySegment<byte>(chunk), SocketFlags.None);
                        if(rs == 0)
                        {
                            Console.WriteLine("Send failed 2");
                        }
                    }
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }
            else
            {
                Console.WriteLine("Failed to connect to server");
            }
        }
        public async Task P2PConnection()
        {

        }
        public byte[] GenerateRemoteRoom()
        {
            byte[] buffer = new byte[10];
            buffer[0] = (byte)DataSendType.INIT;
            buffer[1] = (byte)ConnectType.PARTNER;
            string Id = "11111111";
            byte[] IdBytes = Encoding.ASCII.GetBytes(Id);
            Buffer.BlockCopy(IdBytes, 0, buffer, 2, IdBytes.Length);
            return buffer;
        }
        public byte[] GrabDeskTop()
        {
            Rectangle bound = Screen.PrimaryScreen.Bounds;
            Bitmap screenshot = new Bitmap(bound.Width, bound.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            Graphics graphics = Graphics.FromImage(screenshot);
            graphics.CopyFromScreen(bound.X, bound.Y, 0, 0, bound.Size, CopyPixelOperation.SourceCopy);

            using(MemoryStream stream = new MemoryStream())
            {
                screenshot.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }

        public async Task ReceiveCallback(Socket sck)
        {

            while (true)
            {
                byte[] buffer = new byte[4096];
                int num = await ReceiveAsync(sck, buffer, 0, 4096, SocketFlags.None);
                if (num > 0)
                {
                    byte[] size = new byte[num];
                    Buffer.BlockCopy(buffer, 0, size, 0, num);
                    Console.WriteLine("Response: " + Encoding.ASCII.GetString(size));
                }
                await Task.Delay(100);
            }
        }

        public static Task<int> ReceiveAsync(Socket socket, byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            var tcs = new TaskCompletionSource<int>();
            socket.BeginReceive(buffer, offset, size, socketFlags, ar => {
                try
                {
                    tcs.TrySetResult(socket.EndReceive(ar));
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }, state: null);
            return tcs.Task;
        }
        #endregion


    }
    public enum DataSendType:int
    {
        INIT = 1,
        KEYBOARD = 2,
        SCREEN = 3,
        CHUNK = 4,
        FILE = 5,
        CHAT = 6,
        CONTROL = 7,
    }
    public enum ConnectType:int
    {
        OWNER = 0,
        PARTNER = 1,
    }
    public class RemoteControlResponseType
    {
        public RemoteControlResponseType()
        {
        }
        public RemoteControlResponseType(int code, string message)
        {
            Code = code;
            Message = message;
        }
        public int Code { get; set; } = 0;
        public string Message { get; set; } = string.Empty;
    }
}
