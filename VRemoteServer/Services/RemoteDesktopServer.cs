using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.Utils;
using static VRemoteServer.Utils.Enums;

namespace VRemoteServer.Services
{
    public class RemoteDesktopServer : IDisposable
    {
        private readonly string DEFAULT_SOCKETID = "00000000";
        private ConcurrentDictionary<string, ClientInfo> _clientsActing = new ConcurrentDictionary<string, ClientInfo>();
        private ConcurrentDictionary<string, ConnectionInfo> _connections = new ConcurrentDictionary<string, ConnectionInfo>();

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
            await foreach (var task in _taskReader.ReadAllAsync(cancellation))
            {
                try
                {
                    switch (task.CommandType)
                    {
                        case Enums.CommandType.Connect:
                            break;
                        case Enums.CommandType.Login:
                            await ProcessLogin(task);
                            break;
                        case Enums.CommandType.P2PRequestConnect:
                            await ProcessP2PRequestConnect(task);
                            break;
                        case Enums.CommandType.P2PAcceptConnect:
                            await ProcessRespondP2PRequestConnect(task);
                            break;
                        case Enums.CommandType.P2PRejectConnect:
                            await ProcessRespondP2PRequestConnect(task);
                            break;
                        case Enums.CommandType.P2PDataSend:
                            await ProcessP2PDataSend(task);
                            break;
                        case Enums.CommandType.Disconnect:
                            break;
                        case Enums.CommandType.Ping:
                            ProcessPing(task);
                            break;
                        case Enums.CommandType.Pong:
                            break;
                        case Enums.CommandType.Error:
                            break;
                        case Enums.CommandType.Screen:
                        case Enums.CommandType.Chunks:
                            await P2PDataSend(task);
                            break;
                        case Enums.CommandType.Keyboard:
                        case Enums.CommandType.Mouse:
                        case Enums.CommandType.ScreenOk:
                        case Enums.CommandType.ChunksOk:
                        case Enums.CommandType.Clipboard:
                        case Enums.CommandType.Message:
                        case Enums.CommandType.FileTransfer:
                        case Enums.CommandType.RequestSendFile:
                        case Enums.CommandType.AcceptSendFile:
                            await P2PCommand(task);
                            break;
                        case Enums.CommandType.P2PDisconnect:
                            await ProcessP2PDisconnect(task);
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.ForContext("FileName", "RemoteDesktopServer").Error(ex, $"Error processing task");
                }
            }
        }


        private async Task ProcessP2PDisconnect(RemoteTask task)
        {
            try
            {
                string partnetId = Encoding.ASCII.GetString(task.Data, 5, 8);

                if (_clientsActing.TryGetValue(partnetId, out var client))
                {
                    int result = await Send(client.Client, task.Data);
                }
                else
                {
                    //Todo
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                                   .Error(ex, "P2PDisconnect error");
            }
        }
        private async Task P2PCommand(RemoteTask task)
        {
            if (_connections.TryGetValue(task.PartnerId, out var connection))
            {
                var client = (task.Client == connection.Sender) ? connection.Receiver : connection.Sender;
                await Send(client, task.Data);
            }
        }
        private async Task P2PDataSend(RemoteTask task)
        {
            if (_connections.TryGetValue(task.PartnerId, out var connection))
            {
                var client = (connection.Sender == task.Client) ? connection.Receiver : connection.Sender;
                await Send(client, task.Data);
            }
        }
        public async Task<bool> ProcessDataCallback(string partnerId, Enums.CommandType commandType, Client client, byte[] buffer)
        {
            await Enqueue(new RemoteTask
            {
                PartnerId = partnerId,
                CommandType = commandType,
                Client = client,
                Data = buffer
            });
            return true;
        }
        private async Task<int> SendDataAsync(Client client, byte[] data)
        {
            try
            {
                int response = await client.Socket.SendAsync(data, SocketFlags.None);
                return response;
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"Error when send data to client: {client.IP}", ex.Message);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error("Unexpected error", ex.Message);
            }
            return 0;
        }
        private async Task<int> Send(Client client, byte[] data)
        {
            try
            {

                int response = await client.Socket.SendAsync(data, SocketFlags.None);
                return response;
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, $"Error when send command to client: {client.IP}");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, "Unexpected error");
            }
            return 0;
        }
        private async Task<int> SendCommandAsync(Client client, string socketId, Enums.CommandType commandType, byte[] data)
        {
            try
            {
                byte[] bytes = new byte[data.Length + 5 + socketId.Length];
                Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, bytes, 0, 4);
                bytes[4] = (byte)commandType;
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(socketId), 0, bytes, 5, socketId.Length);
                Buffer.BlockCopy(data, 0, bytes, 5 + socketId.Length, data.Length);

                int response = await client.Socket.SendAsync(bytes, SocketFlags.None);
                return response;
            }
            catch (SocketException ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, $"Error  when send command to client: {client.IP}");
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, "Unexpected error");
            }
            return 0;
        }
        private async Task ProcessP2PDataSend(RemoteTask task)
        {
            string id = Encoding.ASCII.GetString(task.Data, 5, 8);
            if (_connections.TryGetValue(id, out var connection))
            {
                var client = (task.Client == connection.Sender) ? connection.Receiver : connection.Sender;
                await SendCommandAsync(client, task.PartnerId, Enums.CommandType.P2PDataSend, task.Data.Skip(13).ToArray());
            }
        }

        public async Task ProcessRespondP2PRequestConnect(RemoteTask task)
        {
            if (task.CommandType == Enums.CommandType.P2PAcceptConnect)
            {
                if (_connections.TryGetValue(task.PartnerId, out var connection))
                {
                    connection.Receiver = task.Client;
                    await Send(connection.Sender, task.Data);
                }
            }
            else
            {
                string connectionId = Encoding.ASCII.GetString(task.Data);
                try
                {
                    if (_connections.TryGetValue(connectionId, out var connection))
                    {
                        await SendCommandAsync(connection.Sender, connectionId, Enums.CommandType.P2PRejectConnect, new byte[0]);
                    }
                }
                finally
                {
                    _connections.TryRemove(connectionId, out _);
                }
            }

        }


        public async Task ProcessP2PRequestConnect(RemoteTask task)
        {
            if (_clientsActing.TryGetValue(task.PartnerId, out var client))
            {
                string connectionId = Encoding.ASCII.GetString(task.Data, 13, 8);
                ConnectionInfo connection = new ConnectionInfo(connectionId: connectionId, sender: task.Client);
                _connections.TryAdd(connectionId, connection);
                await Send(client.Client, task.Data);
            }
            else
            {
                await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.Error, Encoding.ASCII.GetBytes(nameof(ProcessP2PRequestConnect)));
            }
        }
        public async Task ProcessLogin(RemoteTask task)
        {
            try
            {
                byte[] data = new byte[task.Data.Length - 13];
                Buffer.BlockCopy(task.Data, 13, data, 0, task.Data.Length - 13);

                IPEndPoint ep = task.Client.Socket.RemoteEndPoint as IPEndPoint;

                var clientInfo = Encoding.ASCII.GetString(data).Replace(" ", "").Split('|');
                if (clientInfo.Length != 10)
                {
                    await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.LoginFailed, new byte[0]);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"Invalid login data from client: {ep.Address}");
                }

                var isNullOrEmpty = clientInfo.All(x => x != null);
                if (!isNullOrEmpty)
                {
                    await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.LoginFailed, new byte[0]);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"Invalid login data from client: {ep.Address}");
                }
                if (clientInfo[0].Length != 8)
                {
                    await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.LoginFailed, new byte[0]);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"Invalid login data from client: {ep.Address}");
                }
                if (clientInfo[1].Length != 4)
                {
                    await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.LoginFailed, new byte[0]);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"Invalid login data from client: {ep.Address}");
                }

                ClientInfo loginInfo = new ClientInfo
                {
                    Id = clientInfo[0],
                    Password = clientInfo[1],
                    ComputerName = clientInfo[2],
                    Width = int.Parse(clientInfo[3]),
                    Height = int.Parse(clientInfo[4]),
                    MajorVersion = clientInfo[5],
                    MinorVersion = clientInfo[6],
                    Ip = ep.Address.ToString(),
                    PublicIP = ep.Address.ToString(),
                    Port = ep.Port.ToString(),
                    Client = task.Client
                };
                _clientsActing.TryAdd(loginInfo.Id, loginInfo);

                byte[] bytesInfo = Encoding.ASCII.GetBytes(loginInfo.PublicIP);
                await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.Login, bytesInfo);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                       .Error(ex, "ProcessLogin error");
            }
        }
        public void ProcessData(Client client, byte[] data)
        {
            // Handle data processing logic here
        }
        public async void ProcessPing(RemoteTask task)
        {
            await SendCommandAsync(task.Client, task.PartnerId, Enums.CommandType.Pong, new byte[0]);
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
            try
            {
                var a = _clientsActing.FirstOrDefault(x => x.Value.Client == client);
                if (a.Value != null)
                {
                    _clientsActing.TryRemove(a.Key, out _);
                }
                foreach (var connection in _connections.ToArray())
                {
                    var partner = (connection.Value.Sender == client) ? connection.Value.Receiver : connection.Value.Sender;
                    _ = await SendCommandAsync(partner, DEFAULT_SOCKETID, Enums.CommandType.P2PDisconnect, new byte[0]);
                    if (connection.Value.Sender == client || connection.Value.Receiver == client)
                    {
                        _connections.TryRemove(connection.Key, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                       .Error(ex, "ClientDisconnectCallback error");
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
