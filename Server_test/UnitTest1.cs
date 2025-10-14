using Moq;
using NUnit.Framework;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.RelayServer.Networking;
using VRemoteServer.RelayServer.Services;
using VRemoteServer.Utils;

namespace Server_test
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }
        [Test]
        public void InitServerTest()
        {
            var login = new Mock<ILoginManagerService>();
            login.Setup(x => x.InitServer());

            var remote = new Mock<IRemoteControlManagerService>().Object;
            var relay = new RelayServerManagerService(login.Object, remote);
            relay.InitLoginServer();
        }
        [Test]
        public async Task TestServerBearing()
        {
            ILoginServer x= new LoginServer(50, 50 * 1024);
            ILoginManager y = new LoginManager();

            ILoginManagerService login = new LoginManagerService(x,y);
            IRemoteControlManagerService remote = new Mock<IRemoteControlManagerService>().Object;
            var relayServer = new RelayServerManagerService(login, remote);
            IPEndPoint loginEP = new IPEndPoint(IPAddress.Any, 2399);

            relayServer.InitLoginServer();
            _ =  relayServer.StartLoginServer(loginEP);

            await Task.Delay(500);

            int numberOfConnections = 55;
            List<Socket> sockets = new List<Socket>();
            while (numberOfConnections > 0)
            {
                try
                {
                    Socket sck = await Login();
                    sockets.Add(sck);
                    await Task.Delay(100);

                    numberOfConnections -= 1;

                    Console.WriteLine("Number of users logged: " + relayServer.NumberOfLoginUsers);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }
        private async Task<Socket> Login()
        {
            try
            {
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.NoDelay = true;
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("27.0.12.78"), 2399);
                await socket.ConnectAsync(remoteEP);

                ////Init client info
                //var computerName = Environment.MachineName;
                //int width = 1080;
                //int height = 765;
                //OperatingSystem os = Environment.OSVersion;
                //Random rd = new Random();
                //ClientInfo info = new ClientInfo
                //{
                //    Id = rd.Next(10000000, 99999999).ToString(),
                //    Password = "1111",
                //    ComputerName = computerName,
                //    Width = width,
                //    Height = height,
                //    MajorVersion = os.Version.Major.ToString(),
                //    MinorVersion = os.Version.Minor.ToString(),
                //    Ip = "192.168.1.122",
                //    Port = "2399",
                //    PublicIP = null
                //};
                //byte[] encoder = Encoding.ASCII.GetBytes(info.ToNetworkString());

                ////Prepare packet sending
                //byte[] packet = new byte[13 + encoder.Length];
                //int length = encoder.Length + 13;
                //Buffer.BlockCopy(BitConverter.GetBytes(length), 0, packet, 0, 4);
                //packet[4] = 0x01;

                //Buffer.BlockCopy(Encoding.ASCII.GetBytes("00000000"), 0, packet, 5, 8);
                //Buffer.BlockCopy(encoder, 0, packet, 13, encoder.Length);

                //socket.BeginSend(packet, 0, packet.Length, SocketFlags.None, (IAsyncResult) =>
                //{
                //    int sent = socket.EndSend(IAsyncResult);
                //    Console.WriteLine("Sent " + sent + " bytes");
                //}, null);

                return socket;
            }
            catch(Exception ex)
            {
                Console.WriteLine("Error: ", ex);
            }
            return null;
        }
    }
}