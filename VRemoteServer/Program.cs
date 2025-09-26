using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Net;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.Networking;
using VRemoteServer.RelayServer.Services;
using VRemoteServer.Utils;

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
                    //services.AddScoped<RemoteDesktopServer>();
                    //services.AddScoped<SocketListener>();
                    //services.AddScoped<IServer, Server>();
                    services.AddScoped<ILoginServer>(sp =>new LoginServer(5, 1024 * 16));
                    services.AddScoped<IRemoteControlServer>(sp => new RemoteControlServer(5, 1024 * 16));
                    services.AddScoped<IRemoteConnectionManager, RemoteConnectionManager>();
                    services.AddScoped<ISocketConnectionManager, SocketConnectionManager>();
                    services.AddScoped<ILoginManager, LoginManager>();
                    services.AddScoped<IRemoteControlManager, RemoteControlManager>();
                    services.AddSingleton<IRelayServerManager, RelayServerManager>();
                })
                .Build();

            //var listener = host.Services.GetRequiredService<SocketListener>();
            //await listener.Listen();

            var server = host.Services.GetRequiredService<IRelayServerManager>();
            IPEndPoint loginEP = new IPEndPoint(IPAddress.Any, 2399);
            IPEndPoint remoteControlEP = new IPEndPoint(IPAddress.Any, 2400);
            try
            {
                server.InitLoginServer();
                server.InitRemoteControlServer();

                var loginTask = server.StartLoginServer(loginEP);
                var remoteTask = server.StartRemoteControlServer(remoteControlEP);

                await Task.WhenAll(loginTask, remoteTask);
            }
            finally
            {
                server.CancelLoginServer();
                server.CancelRemoteControlServer(); 
            }
        }
    }
}
