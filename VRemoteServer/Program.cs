using Serilog;
using System;
using System.Threading.Tasks;
using VRemoteServer.Services;
using VRemoteServer.Utils;

namespace VRemoteServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Config();
            Log.Information("Start Service");
            await SocketListener.Listen();
        }
    }
}
