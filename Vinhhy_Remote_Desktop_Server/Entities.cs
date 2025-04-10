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
        public void Add(string id, Socket owner, Socket partner)
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
        public Remote(string id, Socket owner, Socket partner)
        {
            ConnectionId = id;
            Owner = owner;
            Partner = partner;
            LastOwnerPing = null;
            LastPartnerPing = null;
        }
        public string ConnectionId { get; set; }
        public Socket Partner { get; set; }
        public Socket Owner { get; set; }
        public DateTime? LastPartnerPing { get; set; }
        public DateTime? LastOwnerPing { get; set; }

        public void Dispose()
        {
            Owner?.Close();
            Partner?.Close();
            Owner?.Dispose();
            Partner?.Dispose();
        }
    }
    public class SocketClient:IDisposable
    {
        public SocketClient(Socket sck, Action<Socket,byte[], int> callback = null)
        {
            Socket = sck;
            DataReceivedCallback = callback;
        }
        public Action<Socket,byte[], int> DataReceivedCallback { get; set; }
        public Socket Socket { get; set; }
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
            DataReceivedCallback?.Invoke(this.Socket,buffer, byteRead);
        }
        public async Task StartReceiving()
        {
            byte[] buffer = new byte[65536];

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
