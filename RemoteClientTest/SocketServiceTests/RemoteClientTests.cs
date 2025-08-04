using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using VRemoteClient.Services.SocketService;

namespace RemoteClientTest.SocketServiceTests
{
    public class RemoteClientTests
    {
        private RemoteClient _client;
        private ManualResetEvent _resetEvent;

        [SetUp]
        public void Setup()
        {
            var info = VRemoteClient.Utils.Extensions.InitInfo();
            _client = new RemoteClient(info);
            _resetEvent = new ManualResetEvent(false);

            _client.Worker = new BackgroundWorker();
            _client.Worker.WorkerSupportsCancellation = true;
            _client.ConnectEvent += () =>
            {
                _resetEvent.Set();
            };
        }

        private void OnConnected()
        {
            Assert.Pass();
        }

        [Test]
        public void Test()
        {
            Assert.Pass();
        }
        [Test]
        [TestCase("27.0.12.78", 2399)]
        [Timeout(15000)]
        public void RemoteClient_Connect_To_Server_With_ValidParameters(string host, int port)
        {
            //ARRANGE
            _resetEvent.Reset();

            //ACT
            _client.Connect(host, port);
            bool wasSignaled = _resetEvent.WaitOne(5000);

            //ASSERT
            Assert.IsTrue(wasSignaled, "ConnectEventHandler was not triggered within timeout.");
            Assert.IsTrue(_client.SocketConnected, "Socket should be connected");
        }
        [Test]
        [TestCase("8.8.8.8", 2399)]
        [Timeout(15000)]
        public void RemoteClient_Connect_To_Server_With_InvalidParameters(string host, int port)
        {
            //ARRANGE
            _resetEvent.Reset();

            //ACT
            _client.Connect(host, port);
            bool wasSignaled = _resetEvent.WaitOne(5000);

            //ASSERT
            Assert.IsFalse(wasSignaled, "ConnectEventHandler was not triggered within timeout.");
            Assert.IsFalse(_client.SocketConnected, "Socket should be connected");
        }
        [Test]
        [TestCase("8.8.8.8", -2399)]
        public void RemoteClient_Connect_To_Server_With_NeigativeParameters(string host, int port)
        {
            //ARRANGE
            _resetEvent.Reset();

            //ACT
            _client.Connect(host, port);
            bool wasSignaled = _resetEvent.WaitOne(5000);

            //ASSERT
            //Assert.Throws<ArgumentException>(() => _client.Connect(null, 0));
            Assert.IsTrue(IPAddress.TryParse(host, out _), "Host should be a valid IP address.");
            Assert.Positive(port, "Port should be positive value");
        }
    }
}
