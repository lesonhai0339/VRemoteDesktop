using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture;

namespace ScreenCaptureTest
{
    public class ScreenCaptureTest
    {
        [SetUp]
        public void Setup()
        {
        }
        [Test]
        public void Test1()
        {
            var screenCapture = new ScreenCapture();
            int count = 0;
            Stopwatch stopwatch = new Stopwatch();
            while (count < 30)
            {
                stopwatch.Restart();
                var results =  screenCapture.GetScreenTest();
                stopwatch.Stop();
                Console.WriteLine($"Capture {count + 1}: {stopwatch.ElapsedMilliseconds} ms, {results.Count} items");
                count++;
            }
            screenCapture.Dispose();
        }
    }
}
