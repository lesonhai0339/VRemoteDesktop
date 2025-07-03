using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        public void ClientDisconnectCallback(Client client)
        {

        }

        public void Dispose()
        {
            _taskWriter?.Complete();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
