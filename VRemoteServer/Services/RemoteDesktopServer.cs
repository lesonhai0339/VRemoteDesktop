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

namespace VRemoteServer.Services
{
    public class RemoteDesktopServer: IDisposable
    {
        private ConcurrentDictionary<string, ConnectionInfo> RemoteDesktop = new ConcurrentDictionary<string, ConnectionInfo>();
        private ConcurrentDictionary<string ,ClientInfo> _clientsActing = new ConcurrentDictionary<string, ClientInfo>();
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
                            await ProcessLogin(task.Client, task.Data);
                            break;
                        case Enums.CommandType.P2PConnect:
                            await ProcessP2PConnect(task.Client, task.Data);
                            break;
                        case Enums.CommandType.Disconnect:
                            break;
                        case Enums.CommandType.Data:
                            break;
                        case Enums.CommandType.Ping:
                            ProcessPing(task.Client, task.Data);
                            break;
                        case Enums.CommandType.Pong:
                            break;
                        case Enums.CommandType.Error:
                            break;
                        case Enums.CommandType.Screen:
                        case Enums.CommandType.Chunks:
                            await P2PDataSend(task.Client, task.Data);
                            break;
                        case Enums.CommandType.Keyboard:
                        case Enums.CommandType.MouseClick:
                        case Enums.CommandType.MouseMove:
                        case Enums.CommandType.ScreenOk:
                        case Enums.CommandType.ChunksOk:
                            await P2PCommand(task.Client, task.Data);
                            break;
                        case Enums.CommandType.P2PDisconnect:
                            await ProcessP2PDisconnect(task.Client, task.Data);
                            break;
                        case Enums.CommandType.Ack:
                            await SendAck(task.Client, task.Data);
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

        private async Task ProcessP2PDisconnect(Client client, byte[] data)
        {
            try
            {
                await P2PCommand(client, data);
                ClientDisconnectCallback(client);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                                   .Error(ex, "P2PDisconnect error");
            }
        }

        private async Task P2PCommand(Client client, byte[] data)
        {
            var partner = RemoteDesktop.FirstOrDefault(x => x.Value.Sender.Client == client || x.Value.Receiver.Client == client).Value;
            if (partner == null)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"No partner found for client: {client.IP}");
                return;
            }
            else
            {
                await SendDataAsync(partner.Receiver.Client, data);
            }
        }
        private async Task P2PDataSend(Client client, byte[] data)
        {
            var partner = RemoteDesktop.FirstOrDefault(x => x.Value.Sender.Client == client || x.Value.Receiver.Client == client).Value;
            await SendDataAsync(partner.Sender.Client, data);
            //await SendAck(client, new byte[0]);
            partner.Sender.Client._lastSendTime = DateTime.Now;
            partner.Receiver.Client._lastSendTime = DateTime.Now;
        }
        private async Task SendAck(Client client, byte[] data)
        {
            await SendCommandAsync(client, Enums.CommandType.Ack, data);
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
        private async Task<int> SendCommandAsync(Client client,Enums.CommandType commandType, byte[] data= null)
        {
            try
            {
                byte[] bytes= (data != null) ? new byte[data.Length + 5] : new byte[5];
                //4 first bytes is packet length
                Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, bytes, 0, 4);
                // The five byte is the command type, followed by the data length and then the data itself
                bytes[4] = (byte)commandType;
                if (data != null)
                {
                    Buffer.BlockCopy(data, 0, bytes, 5, data.Length);
                }
                int response = await client.Socket.SendAsync(bytes, SocketFlags.None);
                return response;
            }
            catch(SocketException ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, $"Error when send command to client: {client.IP}");
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error(ex, "Unexpected error");
            }
            return 0;
        }

        public async Task ProcessP2PConnect(Client client, byte[] data)
        {
            byte[] x = new byte[data.Length - 5];
            Buffer.BlockCopy(data, 5, x, 0, data.Length -5);
            ClientInfo connecter;
            ClientInfo receiver;
            var receiverData = Encoding.ASCII.GetString(x).Replace(" ", "").Split('|');

            if(receiverData.Length != 3)
            {
                await SendCommandAsync(client, Enums.CommandType.P2PConnectFailed);
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"P2P connect failed. connecter: {client.IP}");
                return;
            }
            else
            {
                connecter = _clientsActing.FirstOrDefault(x=> x.Value.Client == client).Value;

                //current we get three values from receiverData: connecter Id, receive Id, receive Password. i don't  know why i set connecter Id in this case, maybe i will remove it later
                receiver = _clientsActing.FirstOrDefault(x => string.Compare(x.Value.Id, receiverData[1], StringComparison.OrdinalIgnoreCase) == 0
                    && string.Compare(x.Value.Password, receiverData[2], StringComparison.OrdinalIgnoreCase) == 0
                    && x.Value.Client != client).Value;
                if(connecter == null || receiver == null)
                {
                    await SendCommandAsync(client, Enums.CommandType.P2PConnectFailed);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"P2P connect failed. connecter: {client.IP}");
                                        return;
                }
                ConnectionInfo connection = new ConnectionInfo(sender: connecter, receiver: receiver);

                bool flag = RemoteDesktop.TryAdd(connection.SessionId, connection);
                if (flag)
                {
                    //infomation of connector and receiver
                    StringBuilder receiveInfo = new StringBuilder().Append("1").Append("|").Append(connection.SessionId).Append("|").Append(receiver.ToString());
                    StringBuilder connecterInfo = new StringBuilder().Append("0").Append("|").Append(connection.SessionId).Append("|").Append(connecter.ToString());

                    //send the connection information to both clients
                    await SendCommandAsync(connecter.Client, Enums.CommandType.P2PConnect, Encoding.ASCII.GetBytes(receiveInfo.ToString()));
                    await SendCommandAsync(receiver.Client, Enums.CommandType.P2PConnect, Encoding.ASCII.GetBytes(connecterInfo.ToString()));
                }
                else
                {
                    await SendCommandAsync(client, Enums.CommandType.P2PConnectFailed);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"P2P connect failed. connecter: {client.IP}");
                }
            }
        }
        public async Task ProcessLogin(Client client, byte[] data)
        {
            byte[] x = new byte[data.Length - 5];
            Buffer.BlockCopy(data, 5, x, 0, data.Length - 5);
            // Handle login logic here
            IPEndPoint ep = client.Socket.RemoteEndPoint as IPEndPoint;
            
            var clientInfo = Encoding.ASCII.GetString(x).Replace(" ", "").Split('|');
            if(clientInfo.Length != 7)
            {
                await SendCommandAsync(client, Enums.CommandType.LoginFailed);
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"Invalid login data from client: {ep.Address}");
                return;
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
                Port = ep.Port.ToString(),
                Client = client
            };
            _clientsActing.TryAdd(loginInfo.Id, loginInfo);
            await SendCommandAsync(client, Enums.CommandType.Login);

        }
        public void ProcessDisconnect(Client client, byte[] data)
        {
            // Handle disconnect logic here
        }
        public void ProcessData(Client client, byte[] data)
        {
            // Handle data processing logic here
        }
        public async void ProcessPing(Client client, byte[] data)
        {
            await SendCommandAsync(client, Enums.CommandType.Pong);
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
                        int result = await SendCommandAsync(partner.Client, Enums.CommandType.PartnerDisconnected);
                        if (result > 0)
                        {
                            RemoteDesktop.TryRemove(x.Key, out _);
                        }
                    }
                    return true;
                }).ToList();
                await Task.WhenAll(tasks);
            }
            var a = _clientsActing.FirstOrDefault(x => x.Value.Client == client);
            if(a.Value != null)
            {
                _clientsActing.TryRemove(a.Key, out _);
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
