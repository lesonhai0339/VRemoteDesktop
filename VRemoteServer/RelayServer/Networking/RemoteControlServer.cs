using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Common.Options;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;

namespace VRemoteServer.RelayServer.Networking
{
    public interface IRemoteControlServer: IBaseServer<SocketConnection, SocketConnectionEventArg, RemoteControlErrorEventArgs>
    {

    }
    public class RemoteControlServer : BaseServer<SocketConnection, SocketConnectionEventArg, RemoteControlErrorEventArgs>, IRemoteControlServer, IDisposable
    {
        public RemoteControlServer(IRateLimiter rateLimiter, IOptions<RemoteControlServerOptions> options)
            : base(rateLimiter, options.Value.MaxConnections, options.Value.MaxBufferSize) { }
        public override void SendToDomain(SocketConnection domain, int offset, int length)
        {
           domain.CalCuLateData(offset, length);
        }
        public override SocketConnection CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket, EventHandler<SocketConnectionEventArg> dataCallbackEvent)
        {
            SocketConnection connection = new SocketConnection(read, send, socket);
            connection.SocketConnectionEvent += dataCallbackEvent;
            return connection;
        }
        public override (SocketAsyncEventArgs read, SocketAsyncEventArgs send) GetReadAndSendSocketAsyncEventArgsFromDomain(SocketConnection domain)
        {
            if (domain == null) return (null, null);
            return (domain.Reader, domain.Sender);
        }

        public override SocketAsyncEventArgs GetReadSocketAsyncEventArgsFromDomain(SocketConnection domain)
        {
            return domain.Reader;
        }

        public override SocketAsyncEventArgs GetSendSocketAsyncEventArgsFromDomain(SocketConnection domain)
        {
            return domain.Sender;
        }
        public override Socket GetSocketFromDomain(SocketConnection domain)
        {
            return domain.Socket;
        }
        public override SocketConnectionEventType GetEventTypeFromDomainEvent(SocketConnectionEventArg domainEvent)
        {
            return domainEvent.EventType;
        }

        public override void UnRegisterEvent(SocketConnection domain, EventHandler<SocketConnectionEventArg> domainEvent)
        {
            domain.SocketConnectionEvent -= domainEvent;
        }

        public override RemoteControlErrorEventArgs InitException(Exception ex, string note)
        {
            return new RemoteControlErrorEventArgs(ex, note);
        }
        public override void SetTime(SocketConnection domain)
        {
            domain.UpdateTime();
        }
    }
}
