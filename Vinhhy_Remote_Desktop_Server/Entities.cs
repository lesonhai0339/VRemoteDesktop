using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Vinhhy_Remote_Desktop_Server
{
    internal class Entities
    {   
    }
    public class Data
    {
        public Data()
        {

        }
        public Data(string id, RemoteType type, byte[] byteData, SocketResponseType responseType)
        {
            Id = id;
            Type = type;
            ByteData = byteData;
            ResponseType = responseType;
        }
        public string Id { get; set; }

        public SocketResponseType ResponseType { get; set; }
        public RemoteType Type { get; set; }
        public byte[] ByteData { get; set; }
    }
   
    public class RemotesManager
    {
        private Dictionary<string, Remote> _remoteConnections;
        private readonly object _lockObject = new object();

        public RemotesManager()
        {
            _remoteConnections = new Dictionary<string, Remote>();
        }
        public void Add(string id, SocketClient owner, SocketClient partner)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var existingConnection))
                {
                    existingConnection.Owner = owner ?? existingConnection.Owner;
                    existingConnection.Partner = partner ?? existingConnection.Partner;
                }
                else
                {
                    Remote newCon = new Remote(
                        id: id,
                        owner: owner,
                        partner: partner
                    );
                    _remoteConnections[id] = newCon;
                }
            }
        }
        public void Remove(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    connection.Dispose();
                    _remoteConnections.Remove(id);
                }
            }
        }
        public bool IsExisted(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    return true;
                }
                return false;
            }
        }
        public Remote Get(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    return connection;
                }
                return null;
            }
        }
        public bool UpdatePing(string id, RemoteType isOwner)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    if (isOwner == RemoteType.OWNER)
                    {
                        connection.LastOwnerPing = DateTime.Now;
                    }
                    else
                    {
                        connection.LastPartnerPing = DateTime.Now;
                    }
                    return true;
                }
                return false;
            }
        }
        public void Dispose()
        {
            lock (_lockObject)
            {
                foreach (var connection in _remoteConnections.Values)
                {
                    connection.Dispose();
                }
                _remoteConnections.Clear();
            }
        }
    }
    public class Remote : IDisposable
    {
        public Remote()
        {

        }
        public Remote(string id, SocketClient owner, SocketClient partner)
        {
            ConnectionId = id;
            Owner = owner;
            Partner = partner;
            LastOwnerPing = null;
            LastPartnerPing = null;
        }
        public string ConnectionId { get; set; }
        public SocketClient Partner { get; set; }
        public SocketClient Owner { get; set; }

        public DateTime? LastPartnerPing { get; set; }
        public DateTime? LastOwnerPing { get; set; }

        public void Dispose()
        {
            Owner.Socket?.Close();
            Partner.Socket?.Close();
            Owner.Socket?.Dispose();
            Partner.Socket?.Dispose();
        }
    }
    public class SocketClient:IDisposable
    {
        public SocketClient(Socket sck, Action<SocketClient,byte[], int> callback = null)
        {
            Socket = sck;
            DataReceivedCallback = callback;
        }
        public Action<SocketClient, byte[], int> DataReceivedCallback { get; set; }
        public Socket Socket { get; set; }
        public int ByteArrayLength { get; set; } = 0;
        public byte[] ByteArrayBuilder { get; set; }
        private void InitByteData(int length)
        {
            ByteArrayLength = length;
            ByteArrayBuilder = new byte[ByteArrayLength];
        }
        public Task<int> ReceiveAsync(byte[] buffer, int offset, int size, SocketFlags socketFlags)
        {
            var tcs = new TaskCompletionSource<int>();
            Socket.BeginReceive(buffer, offset, size, socketFlags, ar => {
                try
                {
                    tcs.TrySetResult(Socket.EndReceive(ar));
                }
                catch (Exception e)
                {
                    tcs.TrySetException(e);
                }
            }, state: null);
            return tcs.Task;
        }
        public void ProcessDataHandler(byte[] buffer, int byteRead)
        {
            byte[] byteArray = new byte[byteRead];
            Array.Copy(buffer, byteArray, byteRead);
            DataSendType type1 = (DataSendType)buffer[0];
            DataSendType type2 = (DataSendType)buffer[1];
            byte[] dataReceived = byteArray.Skip(2).ToArray();
            switch (type1)
            {
                case DataSendType.INIT:
                    DataReceivedCallback?.Invoke(this, buffer, byteRead);
                    break;
                case DataSendType.KEYBOARD:
                    Console.WriteLine("Keyboard data received");
                    break;
                case DataSendType.SCREEN:
                    byte[] id = new byte[8];
                    byte[] screenLength = new byte[4];
                    Array.Copy(dataReceived, 0, id, 0, 8);
                    Array.Copy(dataReceived, 8, screenLength, 0, 4);
                    InitByteData(BitConverter.ToInt32(screenLength, 0));

                    Console.WriteLine("Screen data received");
                    break;
                case DataSendType.CHUNK:
                    Console.WriteLine("Chunk data received");
                    ByteArrayBuilder.CopyTo(dataReceived, 0);
                    break;
                case DataSendType.FILE:
                    Console.WriteLine("File data received");
                    break;
                case DataSendType.CHAT:
                    Console.WriteLine("Chat data received");
                    break;
                case DataSendType.CONTROL:
                    Console.WriteLine("Control data received");
                    break;
                case DataSendType.DISCONNECT:
                    Console.WriteLine("Socket disconnect");
                    break;
                default:
                    break;
            }

            DataReceivedCallback?.Invoke(this,buffer, byteRead);
        }
        public async Task StartReceiving()
        {
            byte[] buffer = new byte[1024];

            try
            {
                while (true)
                {
                    int byteRead = await ReceiveAsync(buffer, 0, buffer.Length, SocketFlags.None);
                    if (byteRead == 0)
                    {
                        Console.WriteLine("Client Disconnected");
                        break;
                    }
                    ProcessDataHandler(buffer, byteRead);
                }
            }
            catch
            {

            }
            finally
            {
                Socket.Close();
            }
        }
        private bool isDisposed = false;

        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;

            try
            {
                Socket?.Shutdown(SocketShutdown.Both);
            }
            catch { }

            DataReceivedCallback = null;
            Socket?.Close();
            Socket?.Dispose();
        }
    }
}
