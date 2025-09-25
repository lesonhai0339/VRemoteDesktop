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
    public interface ILoginServer: IBaseServer<SocketConnection, LoginEventArgs>
    {

    }
    public class LoginServer : BaseServer<SocketConnection, LoginEventArgs>, ILoginServer, IDisposable
    {
        public LoginServer(int numberOfConnections, int receiveBufferSize)
            : base(numberOfConnections, receiveBufferSize) { }
        public override void SendToDomain(SocketConnection domain, int offset, int length)
        {
            domain.CalCuLateData(offset, length);
        }
        public override void SetFirstPacket(SocketConnection domain)
        {
            domain.IsReceivedFirstPacket = !domain.IsReceivedFirstPacket;
        }
        public override bool ReceivedFirstPacket(SocketConnection domain)
        {
            return domain.IsReceivedFirstPacket;
        }
        public override SocketConnection CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket)
        {
            SocketConnection connection = new SocketConnection(read, send, socket);
            return connection;  
        }

        public override LoginEventArgs CreateEventFromData(ServerEventType type, int offset, int length)
        {
            return new LoginEventArgs(type, offset, length);
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
    }
}
