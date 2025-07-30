using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient;
using VRemoteClient.Services.RemoteDesktopService;

namespace RemoteClientTest.FormRemoteTests
{
    public class FormRemoteTests
    {
        private FormRemote _form;

        [SetUp]
        public void Setup()
        {
            RemoteDesktop remotDesktop = new RemoteDesktop();
            _form = new FormRemote(remotDesktop, 
                new VRemoteClient.Models.Entities.ConnectionInfo 
                { 
                    SessionId = "1111111111111111", 
                    Receiver = null, 
                    Sender = null} 
                );
            _form.Show();
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }
        [Test]
        public void Test2()
        {
            Assert.Pass();
        }
        [TearDown] 
        public void Teardown() 
        {
            Console.WriteLine("Call");
        }
    }
}
