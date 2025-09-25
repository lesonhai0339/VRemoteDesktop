using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Domains;

namespace VRemoteServer.RelayServer.Networking
{
    public interface ILoginServer: IBaseServer<SocketConnection>
    {

    }
    public class LoginServer : BaseServer<SocketConnection>, ILoginServer, IDisposable
    {
        public LoginServer(int numberOfConnections, int receiveBufferSize)
            : base(numberOfConnections, receiveBufferSize) { }
        public override SocketConnection CreateDomainFromSocketAsyncEventArgs(SocketAsyncEventArgs read, SocketAsyncEventArgs send, Socket socket)
        {
            SocketConnection connection = new SocketConnection(read, send, socket);
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
    }
}
