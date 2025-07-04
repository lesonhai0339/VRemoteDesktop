using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.Utils;

namespace VRemoteServer.Services
{
    public class RemoteDesktopServer: IDisposable
    {
        private ConcurrentDictionary<string, ConnectionInfo> RemoteDesktop = new ConcurrentDictionary<string, ConnectionInfo>();
        private Channel<RemoteTask> _taskChanel = Channel.CreateUnbounded<RemoteTask>();
        private ChannelWriter<RemoteTask> _taskWriter;
        private ChannelReader<RemoteTask> _taskReader;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        public RemoteDesktopServer()
        {
            _taskWriter = _taskChanel.Writer;
            _taskReader = _taskChanel.Reader;
            _ = Task.Run(() => DoWorkAsync(_cancellationTokenSource.Token));
        }
        public async Task Enqueue(RemoteTask task)
        {
            await _taskWriter.WriteAsync(task);
        }
        private async Task DoWorkAsync(CancellationToken cancellation)
        {
            await foreach(var task in _taskReader.ReadAllAsync(cancellation))
            {
                try
                {
                    switch (task.CommandType)
                    {
                       
                        case Enums.CommandType.Login:
                            break;
                        case Enums.CommandType.P2PConnect:
                            ProcessP2PConnect(task.Client, task.Data);
                            break;
                        case Enums.CommandType.Disconnect:
                            break;
                        case Enums.CommandType.Data:
                            break;
                        case Enums.CommandType.Ping:
                            break;
                        case Enums.CommandType.Pong:
                            break;
                        case Enums.CommandType.Error:
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", "RemoteDesktopServer").Error($"Error processing task: {ex.Message}");
                }
            }
        }
        public async Task<bool> ProcessDataCallback(Enums.CommandType commandType ,Client client, byte[] buffer)
        {
            Console.WriteLine("Callback");
            await Enqueue(new RemoteTask
            {
                CommandType = commandType,
                Client = client,
                Data = buffer
            });
            return true;
        }
        private async Task SendAsync(Client client, byte[] data)
        {
            try
            {
                await client.Socket.SendAsync(data, SocketFlags.None);
            }
            catch(SocketException ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"Error when send data to client: {client.IP}", ex.Message);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error("Unexpected error", ex.Message);
            }
        }

        public void ProcessP2PConnect(Client client, byte[] data)
        {

        }
        public void ProcessLogin(Client client, byte[] data)
        {
            // Handle login logic here
        }
        public void ProcessDisconnect(Client client, byte[] data)
        {
            // Handle disconnect logic here
        }
        public void ProcessData(Client client, byte[] data)
        {
            // Handle data processing logic here
        }
        public void ProcessPing(Client client, byte[] data)
        {
            // Handle ping logic here
        }
        public void ProcessPong(Client client, byte[] data)
        {
            // Handle pong logic here
        }
        public void ProcessError(Client client, byte[] data)
        {
            // Handle error logic here
        }
        public async void ClientDisconnectCallback(Client client)
        {
            var connections = RemoteDesktop.Where(x => x.Value.Sender.Client == client || x.Value.Receiver.Client == client).ToList();
            if (connections.Any())
            {
                var tasks = connections.Select(async x =>
                {
                    var partner = x.Value.Sender.Client == client ? x.Value.Receiver : x.Value.Sender;
                    if(partner.Client != null)
                    {
                        int result = await partner.Client.Socket.SendAsync(new byte[] { (byte)Enums.CommandType.Disconnect }, SocketFlags.None);
                        if (result > 0)
                        {
                            RemoteDesktop.TryRemove(x.Key, out _);
                        }
                    }
                    return true;
                }).ToList();
                await Task.WhenAll(tasks);
            }

        }
        public void Dispose()
        {
            _taskWriter?.Complete();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
