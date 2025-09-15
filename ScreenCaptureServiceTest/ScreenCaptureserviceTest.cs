using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Utils;
namespace ScreenCaptureServiceTest
{
    public class ScreenCaptureserviceTest
    {
        private IScreenCaptureServiceListener _screenCaptureService;
        [SetUp]
        public void Setup()
        {
            var screenCapture = new Mock<IScreenCapture>();

            screenCapture.Setup(x => x.GetScreen())
                .Returns(new List<ScreenRegion>() {
                    new ScreenRegion{
                        Bytes = new byte[0],
                        IsFullScreen = true,
                        Rectangle = new System.Drawing.Rectangle(new Point(0,0), new Size(0,0)),
                    }
                });

            var capture = new ScreenCapture();
            _screenCaptureService = new ScreenCaptureService(capture);
        }
        [Test]
        public void StartCaptureTest()
        {
            try
            {
                _screenCaptureService.ScreenEvent += (s, e) =>
                {
                    Console.WriteLine(string.Format("Type:{0} - Length:{1} - Time:{2}", e.Type, e.TotalSize, DateTime.Now.ToString("HH:mm:ss:fff")));
                };
                Assert.IsFalse(_screenCaptureService.IsCapturing);
                _screenCaptureService.StartCapture();
                Assert.IsTrue(_screenCaptureService.IsCapturing);
                Thread.Sleep(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartCapture failed: {ex.Message}");
                throw;
            }
        }
        [Test]
        public void StopCaptureTest()
        {
            try
            {
                _screenCaptureService.StartCapture();
                Thread.Sleep(100);
                Assert.IsTrue(_screenCaptureService.IsCapturing);
                _screenCaptureService.StopCapture();
                Thread.Sleep(100);
                Assert.IsFalse(_screenCaptureService.IsCapturing);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Stop capture error: ", ex);
                throw;
            }
        }
        [Test]
        public void GetScreenPacketsTest_ValueNotNull()
        {
            var screenCaptureService = new Mock<IScreenCaptureServiceListener>();
            screenCaptureService.Setup(x => x.GetScreenPackets())
                .Returns(() => new List<byte[]>());

            var value = screenCaptureService.Object.GetScreenPackets();
            Assert.IsNotNull(value);
        }
        [Test]
        public void GetScreenPacketsTest_ValueIsNull()
        {
            var screenCaptureService = new Mock<IScreenCaptureServiceListener>();
            screenCaptureService.Setup(x => x.GetScreenPackets())
                .Returns(() => null);

            var value = screenCaptureService.Object.GetScreenPackets();
            Assert.IsNull(value);
        }
    }
}