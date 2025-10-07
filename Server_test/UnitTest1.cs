using Moq;
using NUnit.Framework;
using VRemoteServer.RelayServer.Services;

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
    }
}