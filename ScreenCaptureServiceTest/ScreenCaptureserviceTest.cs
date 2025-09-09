using Moq;
using NUnit.Framework;
using System;
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
        public void TestDoWork()
        {
            var mockScreenCapture = new Mock<IScreenCapture>();
            var mockConfig = new Mock<ScreenCaptureConfig>();

            var screenCaptureService = new ScreenCaptureService(null, null);
            screenCaptureService.ScreenEvent += (s, e) =>
            {
                Console.WriteLine($"ScreenEvent received: {e.Type} at: {DateTime.Now.ToString("HH:mm:ss:fff")}");
            };
            try
            {
                Assert.False(screenCaptureService.IsCapturing);
                screenCaptureService.StartCapture();
                Assert.True(screenCaptureService.IsCapturing);
                Thread.Sleep(30000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StartCapture failed: {ex.Message}");
                throw;
            }
        }
    }
}