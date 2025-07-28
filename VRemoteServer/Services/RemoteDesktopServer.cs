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
    public class RemoteDesktopServer: IDisposable
    {
        private readonly string defaultSessionId = "0000000000000000";
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
                            await ProcessLogin(task);
                            break;
                        case Enums.CommandType.P2PConnect:
                            await ProcessP2PConnect(task);
                            break;
                        case Enums.CommandType.Disconnect:
                            break;
                        case Enums.CommandType.Data:
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
                            await P2PCommand(task);
                            break;
                        case Enums.CommandType.P2PDisconnect:
                            await ProcessP2PDisconnect(task);
                            break;
                        case Enums.CommandType.Ack:
                            await SendAck(task);
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
                var rooms = RemoteDesktop.Where(x => string.Compare(x.Key, task.SessionId, StringComparison.OrdinalIgnoreCase) == 0).ToList();
                if (rooms.Count > 0)
                {
                    var tasks = rooms.Select(async room =>
                    {
                        var partner = (room.Value.Sender.Client == task.Client) ? room.Value.Receiver : room.Value.Sender;
                        if (partner.Client != null)
                        {
                            int result = await SendCommandAsync(room.Value.Receiver.Client, Enums.CommandType.P2PDisconnect);
                            int result2 = await SendCommandAsync(room.Value.Sender.Client, Enums.CommandType.P2PDisconnect);
                            if (result > 0 && result2 > 0)
                            {
                                room.Value.Sender.Client.ClearHeader();
                                room.Value.Receiver.Client.ClearHeader();
                                RemoteDesktop.TryRemove(room.Key, out _);
                            }
                        }
                        return true;
                    }).ToList();
                    await Task.WhenAll(tasks);
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                                   .Error(ex, "P2PDisconnect error");
            }
        }
        private async Task P2PCommand(RemoteTask task)
        {
            var room = RemoteDesktop.FirstOrDefault(x => 
                string.Compare(x.Key, task.SessionId, StringComparison.OrdinalIgnoreCase) == 0).Value;
            if (room == null)
            {
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"Invalid remote connection");
                return;
            }
            else
            {
                var partner = (room.Sender.Client == task.Client) ? room.Receiver : room.Sender;
                await SendDataAsync(partner.Client, task.Data);
            }
        }
        private async Task P2PDataSend(RemoteTask task)
        {
            var room = RemoteDesktop.FirstOrDefault(x => string.Compare(x.Key, task.SessionId, StringComparison.OrdinalIgnoreCase) == 0).Value;
            var partner = (room.Sender.Client == task.Client) ? room.Receiver : room.Sender;  

            await SendDataAsync(partner.Client, task.Data);

            //reset timeout
            room.Sender.Client._lastSendTime = DateTime.Now;
            room.Receiver.Client._lastSendTime = DateTime.Now;
        }
        private async Task SendAck(RemoteTask task)
        {
            await SendCommandAsync(task.Client, Enums.CommandType.Ack, task.Data);
        }
        public async Task<bool> ProcessDataCallback(string sessionId, Enums.CommandType commandType ,Client client, byte[] buffer)
        {
            await Enqueue(new RemoteTask
            {
                SessionId = sessionId,
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
                // Some packets only contain a header without any data
                byte[] bytes = (data != null) ? new byte[data.Length + 21] : new byte[21];
                //sessionId
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(defaultSessionId), 0, bytes, 0, 16);
                //data length
                Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length), 0, bytes, 16, 4);
                //data type
                bytes[20] = (byte)commandType;
                if (data != null)
                {
                    //if 'data' is not null, copy it into 'bytes'
                    Buffer.BlockCopy(data, 0, bytes, 20, data.Length);
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

        public async Task ProcessP2PConnect(RemoteTask task)
        {
            ClientInfo connecter;
            ClientInfo receiver;

            byte[] data = new byte[task.Data.Length - 21];
            Buffer.BlockCopy(task.Data, 21, data, 0, task.Data.Length - 21);
            var receiverData = Encoding.ASCII.GetString(data).Replace(" ", "").Split('|');

            // Includes 3 parameters: senderId, receiverId, and receiverPassword
            if (receiverData.Length != 3)
            {
                await SendCommandAsync(task.Client, Enums.CommandType.P2PConnectFailed);
                Log.ForContext("FileName", "RemoteDesktopServer")
                    .Error($"P2P connect failed. connecter: {task.Client.IP}");
                return;
            }
            else
            {
                // Find 'Sender' in active socket
                connecter = _clientsActing.FirstOrDefault(x=> x.Value.Client == task.Client).Value;

                //Find 'Receiver' in active socket
                receiver = _clientsActing.FirstOrDefault(x => string.Compare(x.Value.Id, receiverData[1], StringComparison.OrdinalIgnoreCase) == 0
                    && string.Compare(x.Value.Password, receiverData[2], StringComparison.OrdinalIgnoreCase) == 0
                    && x.Value.Client != task.Client).Value;

                if(connecter == null || receiver == null)
                {
                    await SendCommandAsync(task.Client, Enums.CommandType.P2PConnectFailed);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"P2P connect failed. connecter: {task.Client.IP}");
                                        return;
                }

                // Create a connection between two sockets, considered as a remote desktop room
                ConnectionInfo connection = new ConnectionInfo(sender: connecter, receiver: receiver);

                bool flag = RemoteDesktop.TryAdd(connection.SessionId, connection);
                if (flag)
                {
                    // 'Receiver' information will be sent to the 'Sender'
                    StringBuilder receiveInfo = new StringBuilder()
                        .Append((int)Connecter.Receiver).Append("|")
                        .Append(connection.SessionId).Append("|")
                        .Append(receiver.ToString());

                    // 'Sender' information will be sent to the 'Receiver'
                    StringBuilder connecterInfo = new StringBuilder()
                        .Append((int)Connecter.Sender).Append("|")
                        .Append(connection.SessionId).Append("|")
                        .Append(connecter.ToString());

                    //Send data to both sockets
                    //Send to 'Sender'
                    await SendCommandAsync(connecter.Client, Enums.CommandType.P2PConnect, Encoding.ASCII.GetBytes(receiveInfo.ToString()));
                    //Send to 'Receiver'
                    await SendCommandAsync(receiver.Client, Enums.CommandType.P2PConnect, Encoding.ASCII.GetBytes(connecterInfo.ToString()));
                }
                else
                {
                    await SendCommandAsync(task.Client, Enums.CommandType.P2PConnectFailed);
                    Log.ForContext("FileName", "RemoteDesktopServer")
                        .Error($"P2P connect failed. connecter: {task.Client.IP}");
                }
            }
        }
        public async Task ProcessLogin(RemoteTask task)
        {
            try
            {
                // Ignore the first 21 bytes: sessionId (not present during login), data length, and data type
                byte[] data = new byte[task.Data.Length - 21];
                Buffer.BlockCopy(task.Data, 21, data, 0, task.Data.Length - 21);

                // Get the IPEndPoint of the current socket
                IPEndPoint ep = task.Client.Socket.RemoteEndPoint as IPEndPoint;

                var clientInfo = Encoding.ASCII.GetString(data).Replace(" ", "").Split('|');
                if (clientInfo.Length != 7)
                {
                    await SendCommandAsync(task.Client, Enums.CommandType.LoginFailed);
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
                    Client = task.Client
                };
                _clientsActing.TryAdd(loginInfo.Id, loginInfo);
                await SendCommandAsync(task.Client, Enums.CommandType.Login);
            }
            catch(Exception ex)
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
            await SendCommandAsync(task.Client, Enums.CommandType.Pong);
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
                var connections = RemoteDesktop.Where(x => x.Value.Sender.Client == client || x.Value.Receiver.Client == client).ToList();
                if (connections.Any())
                {
                    var tasks = connections.Select(async x =>
                    {
                        var partner = x.Value.Sender.Client == client ? x.Value.Receiver : x.Value.Sender;
                        if (partner.Client != null)
                        {
                            int result = await SendCommandAsync(partner.Client, Enums.CommandType.P2PDisconnect);
                            if (result > 0)
                            {
                                partner.Client.ClearHeader();
                                client.ClearHeader();
                                RemoteDesktop.TryRemove(x.Key, out _);
                            }
                        }
                        return true;
                    }).ToList();
                    await Task.WhenAll(tasks);
                }
                var a = _clientsActing.FirstOrDefault(x => x.Value.Client == client);
                if (a.Value != null)
                {
                    _clientsActing.TryRemove(a.Key, out _);
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
