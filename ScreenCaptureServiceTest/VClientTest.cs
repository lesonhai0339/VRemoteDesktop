using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.VTCPClient;

namespace ScreenCaptureTest
{
    public class VClientTest
    {
        private VClient _vclient;
        [SetUp]
        public void Setup()
        {
            string sckId = Guid.NewGuid().ToString().Substring(0, 8);
            VClientType clientType = VClientType.Sender;
            _vclient = new VClient(sckId, clientType);
        }
        [TestCase("27.0.12.78", 2399)]
        public void Connect_Test(string ip, int port)
        {
            _vclient.Connect(ip, port);
            Thread.Sleep(1000);
            Assert.IsTrue(_vclient.SocketConnected);
        }
        [Test]
        [TestCase("", 0)]
        [TestCase(null, 0)]
        [TestCase("", 0)]
        [TestCase("", -int.MaxValue)]
        [TestCase("27.0.12.78", 0)]
        [TestCase(null, 2399)]
        public void Connect_Test_Wrong_Arguments(string ip, int port)
        {
            _vclient.Connect(ip, port);
            Thread.Sleep(1000);
            Assert.IsFalse(_vclient.SocketConnected);
        }
    }
}
