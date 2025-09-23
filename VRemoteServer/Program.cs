using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;
using VRemoteServer.Services;
using VRemoteServer.Utils;
using static VRemoteServer.Services.RemoteDesktopConnectionServer;

namespace VRemoteServer
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Logger.Config();
            Log.ForContext("FileName", "Main").Information("Start Service");
            //RemoteDesktopServer remoteDesktop = new RemoteDesktopServer();
            //SocketListener socketListener = new SocketListener(remoteDesktop);
            //await socketListener.Listen();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<RemoteDesktopServer>();
                    services.AddSingleton<SocketListener>();
                    services.AddSingleton<Server>();
                })
                .Build();

            //var listener = host.Services.GetRequiredService<SocketListener>();
            //await listener.Listen();


            var listener2 = host.Services.GetRequiredService<Server>();
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 2399);
            listener2.Init();
            listener2.Start(ep);
        }
    }
}
