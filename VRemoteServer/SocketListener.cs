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

namespace VRemoteServer
{
    internal static class SocketListener
    {
        private static readonly SemaphoreSlim _connectionSemaphore = new(200); // Max 200 concurrent
        private static readonly ConcurrentDictionary<string, Client> _clients = new();
        public static async Task Listen()
        {
            //init socket listener and config options
            Socket sck = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);
            sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sck.NoDelay = true;

            //init ipendpoint accept any ip and port 2399
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, 2399);

            //start listen incomming socket
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
                        RemoteDesktopServer.ClientDisconnectCallback, 
                        RemoteDesktopServer.ProcessDataCallback);

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
