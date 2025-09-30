using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;

namespace VRemoteServer.RelayServer.Networking
{
    public interface ILoginServer: IBaseServer<SocketConnection, SocketConnectionEventArg, LoginErrorEventArgs>
    {

    }
    public class LoginServer : BaseServer<SocketConnection, SocketConnectionEventArg, LoginErrorEventArgs>, ILoginServer, IDisposable
    {
        public LoginServer(int numberOfConnections, int receiveBufferSize)
            : base(numberOfConnections, receiveBufferSize) { }
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

        public override LoginErrorEventArgs InitException(Exception ex, string note)
        {
            return new LoginErrorEventArgs(ex, note);
        }
    }
}
