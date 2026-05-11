using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.FileService.DTOs
{
    public class ChatMessage
    {
        public ChatMessage(string savePath, ChatText chatText)
        {
            SavePath = savePath;
            ChatText = chatText;
            ChatFile = null;
        }
        public ChatMessage(string savePath, ChatFile chatFile)
        {
            SavePath = savePath;
            ChatFile = chatFile;
            ChatText = null;
        }
        public string SavePath { get; set; }
        public ChatText ChatText { get; set; }
        public ChatFile ChatFile { get; set; }
    }
}
