using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.DTOs
{
    public static class HeaderSchema
    {
        public const int SessionIdLength = 8;
        public const int RetryAttemp = 3;

        public const string RootFolder = "VRemote";
        public const string Key = "Vinhhy";
        public const string Separator = "|";
        public const char SeparatorChar = '|';
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public const int Timeout = 30;
        public const int TimeoutMilisecond = 3000;

        public const int IdBytes = 8;
        public const int TypeBytes = 1;
        public const int PayloadBytes = 4;
        public const int TotalHeaderSize = PayloadBytes + IdBytes + TypeBytes;

        public const string ConnectionHistoryFile = "connection_history.txt";
        public const string PasswordFile = "ps.txt";

        public const string ChatDirectory = "ChatData";
        public const int LimitChatLineLoaded = 5;

        public const int FileIdBytes = 16;
        public const int FileTimeout = 60;

        public const int FileFieldCount = 5;
        public const int FileIdIndex = 0;
        public const int FileNameIndex = 1;
        public const int FileExtensionIndex = 2;
        public const int FileSizeIndex = 3;
        public const int FileCheckSumIndex = 4;
        public const int FileChunkSize = 32 * 1024; //32KB each chunk


    }
}
