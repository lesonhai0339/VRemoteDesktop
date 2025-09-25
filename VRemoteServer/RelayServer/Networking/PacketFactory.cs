using System;
using System.Text;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Helpers;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Networking
{
    public static class PacketFactory
    {
        public static (int length, SocketDataType type, string connectionId) GetHeaderDataFromPacket(byte[] buffer, int packetOffset, int packetLength)
        {
            if(buffer == null || buffer.Length == 0)
                throw new ArgumentNullException(nameof(buffer));

            if(packetOffset <0)
                throw new ArgumentOutOfRangeException(nameof(packetOffset)); 
            
            if (packetLength < PACKET_HEADER_LENGTH)
                throw new ArgumentOutOfRangeException(nameof(packetLength));

            try
            {
                int offset = packetOffset + PACKET_SIZE_INDEX;
                int payloadLength = packetLength - PACKET_HEADER_LENGTH;

                byte[] data = new byte[payloadLength];

                //Packet size
                int length = BitConverter.ToInt32(buffer, offset);
                offset += PACKET_SIZE_LENGTH;
                if (length != packetLength)
                {
                    throw new ArgumentOutOfRangeException(nameof(length));  
                }

                //Packet type
                SocketDataType type = (SocketDataType)buffer[offset];
                offset += PACKET_TYPE_LENGTH;

                //Connection Id
                string connectionId = Encoding.ASCII.ByteArrayToString(buffer, offset, PACKET_ID_LENGTH);

                return (length, type, connectionId);
            }
            catch(Exception)
            {
                throw;
            }
        }
        public static byte[] CreatePacket(SocketDataType type, string id, byte[] data = null)
        {
            if (type == SocketDataType.None)
                return Array.Empty<byte>();

            if (string.IsNullOrEmpty(id))
                return Array.Empty<byte>();

            int offset = 0;
            int headerLength = PACKET_HEADER_LENGTH;
            int payloadLength = data?.Length ?? 0;
            int packetSize = headerLength + payloadLength;

            byte[] dataSend = new byte[packetSize];

            //Packet size
            Buffer.BlockCopy(BitConverter.GetBytes(packetSize), 0, dataSend, offset, PACKET_SIZE_LENGTH);
            offset += PACKET_SIZE_LENGTH;

            //Packet type
            dataSend[offset] = (byte)type;
            offset += PACKET_TYPE_LENGTH;

            //Id
            Buffer.BlockCopy(Encoding.ASCII.StringToByteArray(id), 0, dataSend, offset, PACKET_ID_LENGTH);
            offset += PACKET_ID_LENGTH;

            //Payload
            if (data != null && data.Length > 0)
            {
                Buffer.BlockCopy(data, 0, dataSend, offset, data.Length);
            }
            return dataSend;
        }
        public static byte[] CreatePacket(SocketDataType type, string id, int dataOffset, int dataLength, byte[] data = null)
        {
            if (type == SocketDataType.None)
                return Array.Empty<byte>();

            if (string.IsNullOrEmpty(id))
                return Array.Empty<byte>();

            int offset = 0;
            int headerLength = PACKET_HEADER_LENGTH;
            int payloadLength = data?.Length ?? 0;
            int packetSize = headerLength + payloadLength;

            byte[] dataSend = new byte[packetSize];

            //Packet size
            Buffer.BlockCopy(BitConverter.GetBytes(packetSize), 0, dataSend, offset, PACKET_SIZE_LENGTH);
            offset += PACKET_SIZE_LENGTH;

            //Packet type
            dataSend[offset] = (byte)type;
            offset += PACKET_TYPE_LENGTH;

            //Id
            Buffer.BlockCopy(Encoding.ASCII.StringToByteArray(id), 0, dataSend, offset, PACKET_ID_LENGTH);
            offset += PACKET_ID_LENGTH;

            //Payload
            if (data != null && data.Length > 0)
            {
                Buffer.BlockCopy(data, dataOffset, dataSend, offset, dataLength);
            }
            return dataSend;
        }
    }
}
