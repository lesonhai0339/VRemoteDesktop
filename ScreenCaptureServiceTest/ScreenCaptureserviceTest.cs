using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ScreenCapture;
namespace ScreenCaptureServiceTest
{
    public class ScreenCaptureserviceTest
    {
        [SetUp]
        public void Setup()
        {
        }
        [Test]
        public void TestCaptureElapsedTime()
        {
            //var screencapture = new Mock<IScreenCapture>();

            //screencapture.Setup(x => x.GetScreen())
            //    .Returns(new List<ScreenRegion> {
            //    new ScreenRegion{
            //        IsFullScreen = true,
            //        Rectangle = new System.Drawing.Rectangle(),
            //        Bytes = new byte[0]
            //    }
            //});

            //var screenCaptureService = new ScreenCaptureService(screencapture.Object);
            var screencapture = new ScreenCapture();

            var screenCaptureService = new ScreenCaptureService(screencapture);
            int count = 0;
            screenCaptureService.ScreenEvent += (s, e) =>
            {
                Console.WriteLine($"ScreenEvent received: {e.Type} at: {DateTime.Now.ToString("HH:mm:ss:fff")}");
                count++;
            };
            try
            {
                Assert.False(screenCaptureService.IsCapturing);
                screenCaptureService.StartCapture();
                Assert.True(screenCaptureService.IsCapturing);
                Thread.Sleep(TimeSpan.FromSeconds(30));
                Console.WriteLine($"Number screen capture call in 30 second with fps {VRemoteDesktop.Utils.DefaultValue.DEFAULT_FPS}: "+ count);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartCapture failed: {ex.Message}");
                throw;
            }
        }
    }
}