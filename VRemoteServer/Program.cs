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
            Log.ForContext("FileName", "Main").Information("Start Service");
            RemoteDesktopServer remoteDesktop = new RemoteDesktopServer();
            SocketListener socketListener = new SocketListener(remoteDesktop);
            await socketListener.Listen();
        }
    }
}
