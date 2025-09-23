using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Networking;
using VRemoteServer.RelayServer.Services;
using VRemoteServer.Utils;

namespace VRemoteServer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Logger.Config();
            Log.ForContext("FileName", "Main").Information("Start Service");
            //RemoteDesktopServer remoteDesktop = new RemoteDesktopServer();
            //SocketListener socketListener = new SocketListener(remoteDesktop);
            //await socketListener.Listen();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    //services.AddScoped<RemoteDesktopServer>();
                    //services.AddScoped<SocketListener>();
                    services.AddScoped<IServer, Server>();
                    services.AddScoped<IRemoteConnectionManager, RemoteConnectionManager>();
                    services.AddScoped<ISocketConnectionManager, SocketConnectionManager>();
                    services.AddSingleton<IRelayServerManager, RelayServerManager>();
                })
                .Build();

            //var listener = host.Services.GetRequiredService<SocketListener>();
            //await listener.Listen();


            var server = host.Services.GetRequiredService<IRelayServerManager>();
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 2399);
            server.InitServer();
            server.StartServer(ep);
        }
    }
}
