using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace RemoteClient
{
    internal class StateObject
    {
        public StateObject()
        {
            WorkSocket = null;
            Buffer = new byte[2049];
            ByteArrayBuilder = new ByteArrayBuilder();
            ColPendingContinousPacket = new Dictionary<byte, ByteArrayBuilder>();
            SckId = "";
        }
        public Socket WorkSocket;
        public const int BufferSize = 2048;
        public byte[] Buffer;
        public ByteArrayBuilder ByteArrayBuilder;
        public Dictionary<byte, ByteArrayBuilder> ColPendingContinousPacket;

        public string SckId;
        public EnumP2PType P2P_Type;
        public enum EnumP2PType
        {
            P2P_LISTENER_SOCKET,
            P2P_CONNECT_SOCKET
        }

    }
}
