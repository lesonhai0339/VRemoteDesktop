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

                }
                catch (Exception ex)
                {
                    Log.Error($"Error processing task: {ex.Message}");
                }
            }
        }
        public async Task<bool> ProcessDataCallback(Enums.CommandType commandType ,Client client, byte[] buffer)
        {
            await Enqueue(new RemoteTask
            {
                CommandType = commandType,
                Client = client,
                Data = buffer
            });
            return true;
        }

        public void ClientConnect(Client client, byte[] data)
        {

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
