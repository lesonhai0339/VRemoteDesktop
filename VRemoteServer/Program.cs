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
using Microsoft.Extensions.Configuration;
using VRemoteServer.RelayServer.DTOs;


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

            //var config = new ConfigurationBuilder()
            //    .SetBasePath(AppContext.BaseDirectory)
            //    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            //    .Build();

            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;

                    //services.AddScoped<RemoteDesktopServer>();
                    //services.AddScoped<SocketListener>();
                    //services.AddScoped<IServer, Server>();

                    services.Configure<LoginServerOptions>(config.GetSection("LoginServerConfig"));
                    services.Configure<RemoteControlServerOptions>(config.GetSection("RemoteServerConfig"));

                    services.AddSingleton<IRateLimiter, RateLimiter>();
                    services.AddSingleton<ILoginServer, LoginServer>();
                    services.AddSingleton<IRemoteControlServer, RemoteControlServer>();
                    services.AddSingleton<ILoginManager, LoginManager>();
                    services.AddSingleton<ILoginManagerService, LoginManagerService>();
                    services.AddSingleton<IRemoteControlManager, RemoteControlManager>();
                    services.AddSingleton<IRemoteControlManagerService, RemoteControlManagerService>();
                    services.AddSingleton<IRelayServerManager, RelayServerManagerService>();
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
