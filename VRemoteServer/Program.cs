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
using System.Threading;
using VRemoteServer.RelayServer.Common.Options;
using VRemoteServer.Server.Options;


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
                .UseWindowsService(options =>
                {
                    options.ServiceName = "VRemoteServer"; 
                })
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;



                    //services.AddScoped<RemoteDesktopServer>();
                    //services.AddScoped<SocketListener>();
                    //services.AddScoped<IServer, Server>();
                    services.Configure<ServerOptions>(config.GetSection("ServerConfig"));
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
                    services.AddHostedService<RelayHostedService>();

                })
                .Build();

            await host.RunAsync();
        }
    }

    internal class RelayHostedService : IHostedService
    {
        private readonly IRelayServerManager _server;
        public RelayHostedService(IRelayServerManager server)
        {
            _server = server;
        }  
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _server.InitLoginServer();
            _server.InitRemoteControlServer();

            var loginEP = new IPEndPoint(IPAddress.Any, 2399);
            var remoteEP = new IPEndPoint(IPAddress.Any, 2400);

            _ = _server.StartLoginServer(loginEP);
            _ = _server.StartRemoteControlServer(remoteEP);

            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Log.Information("Heartbeat: {time}", DateTime.Now);
                    await Task.Delay(30000, cancellationToken);
                }
            });

        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _server.CancelLoginServer();
            _server.CancelRemoteControlServer();

            return Task.CompletedTask;
        }
    }
}
