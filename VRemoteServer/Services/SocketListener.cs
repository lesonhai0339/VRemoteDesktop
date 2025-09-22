using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.Models;

namespace VRemoteServer.Services
{
    public class SocketListener
    {
        private readonly SemaphoreSlim _connectionSemaphore = new(200); // Max 200 concurrent
        private readonly ConcurrentDictionary<string, Client> _clients = new();
        private RemoteDesktopServer _remoteDesktop;
        public SocketListener(RemoteDesktopServer remoteDesktop)
        {
            _remoteDesktop = remoteDesktop;
        }
        public async Task Listen()
        {
            //init socket listener and config options
            Socket sck = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sck.NoDelay = true;

            //init ipEndpoint accept any ip and port 2399
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 2399);

            //start listen inComing socket
            sck.Bind(ep);
            sck.Listen(100);

            try
            {
                while (true)
                {
                    Log.Information("Waiting for a connection...");
                    await _connectionSemaphore.WaitAsync();

                    Socket clientSck = await sck.AcceptAsync();

                    Client client = new Client(clientSck,
                        _remoteDesktop.ClientDisconnectCallback,
                        _remoteDesktop.ProcessDataCallback);

                    _clients.TryAdd(client.IP, client);

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await client.StartReceiving();
                        }
                        finally
                        {
                            _connectionSemaphore.Release();
                            _clients.TryRemove(client.IP, out _);
                            client.Dispose();
                        }
                    });
                    Log.Information("Client connected: {IP}", client.IP);
                }
            }
            catch (SocketException ex)
            {
                Log.Error(ex, "SocketException occurred while listening for connections: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An unexpected error occurred while listening for connections: {Message}", ex.Message);
            }
            finally
            {
                Log.Information("Closing Remote Desktop Server");
            }
        }
    }
}
