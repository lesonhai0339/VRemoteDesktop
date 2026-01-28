using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using VRemoteDesktop.Services.FileService.Enums;

namespace VRemoteDesktop.Services.FileService.DTOs
{
    [DataContract]
    public class ChatFile
    {
        public ChatFile()
        {
        }
        public ChatFile(ChatContentTypeEnum type, ChatOwnerEnum owner, string filePath, string fileName, long fileSize, DateTime time)
        {
            Type = type;
            Owner = owner;
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            Time = time;
        }
        [DataMember(Order = 1)]
        public ChatContentTypeEnum Type { get; set; }

        [DataMember(Order = 2)]
        public ChatOwnerEnum Owner { get; set; }

        [DataMember(Order = 3)]
        public string FilePath { get; set; }

        [DataMember(Order = 4)]
        public string FileName { get; set; }

        [DataMember(Order = 5)]
        public long FileSize { get; set; }

        [DataMember(Order = 0)]
        public DateTime Time { get; set; }
        public string ToDataString()
        {
            var props = GetType()
               .GetProperties(BindingFlags.Public | BindingFlags.Instance)
               .OrderBy(p =>
               {
                   var attr = (DataMemberAttribute)Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute));
                   return attr != null ? attr.Order : int.MaxValue;
               });

            StringBuilder sb = new StringBuilder();
            foreach (var prop in props)
            {
                sb.Append(prop.PropertyType == typeof(DateTime)
                    ? ((DateTime)prop.GetValue(this, null)).ToString()
                    : prop.GetValue(this, null).ToString()
                    ?? string.Empty)
                .Append("|");
            }
            return sb.ToString().TrimEnd(char.Parse("|"));
        }
    }
}
