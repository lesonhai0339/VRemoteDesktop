using Moq;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Services.ScreenCapture;

namespace ScreenCaptureTest
{
    public class ScreenCaptureTest
    {
        private ScreenCapture _screenCapture;
        private ScreenCapture1 _screenCapture1;
        [SetUp]
        public void Setup()
        {
            _screenCapture = new ScreenCapture();
            _screenCapture1 = new ScreenCapture1();
        }
        [Test]
        public void ScreenCapture_TestPerformance()
        {
            int count = 0;
            int stop = 500;
            while(count < stop)
            {
                Stopwatch stopWatch = Stopwatch.StartNew();
                var screens = _screenCapture1.GetScreen();
                stopWatch.Stop();
                var elapsed = stopWatch.Elapsed.TotalMilliseconds;
                Console.WriteLine($"Elapsed: {elapsed} - Count: {count}");
                count++;
            }
        }
        [Test]
        public void GetScreenTest_ValidValues()
        {
            var result = _screenCapture.GetScreen();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            foreach(var region in result)
            {
                Assert.IsNotNull(region);
                Assert.IsTrue(region.TotalSize > 0);
                Assert.IsNotNull(region.Rectangle);
            }
        }
        [Test]
        public void GetCurrentScreenTest_ValidValues()
        {
            var result = _screenCapture.GetCurrentScreen();
            Assert.IsNotNull( result );
            Assert.IsTrue(result.Count > 0);
            foreach(var region in result)
            {
                Assert.IsNotNull(region);
                Assert.IsTrue(region.TotalSize > 0);
                Assert.IsNotNull(region.Rectangle);
            }
        }
        [Test]
        public void RenewTest_MultiThread()
        {
            int num = 10000;
            int calls = 0;
            
            Parallel.For(0, num, i =>
            {
                Interlocked.Increment(ref calls);
                _screenCapture.Renew();
            });

            Assert.AreEqual(num, calls);
        }
    }
}
