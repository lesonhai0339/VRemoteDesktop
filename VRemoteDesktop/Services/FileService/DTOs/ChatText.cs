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
    public class ChatText
    {
        public ChatText()
        {

        }
        public ChatText(ChatContentTypeEnum type, ChatOwnerEnum owner, string message, DateTime time)
        {
            Type = type;
            Owner = owner;
            Message = message;
            Time = time;
        }
        [DataMember(Order = 1)]
        public ChatContentTypeEnum Type { get; set; }

        [DataMember(Order = 2)]
        public ChatOwnerEnum Owner { get; set; }

        [DataMember(Order = 3)]
        public string Message { get; set; }

        [DataMember(Order = 0)]
        public DateTime Time { get; set; }
        public string ToDataString()
        {
            var props = this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(p =>
                {
                    var attr = (DataMemberAttribute)Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute));
                    return attr != null ? attr.Order : int.MaxValue;
                });

            StringBuilder sb = new StringBuilder();
            foreach (var prop in props)
            {
                sb.Append(prop.PropertyType == typeof(DateTime)
                    ? ((DateTime)prop.GetValue(this, null)).ToString("|")
                    : prop.GetValue(this, null).ToString()
                    ?? string.Empty)
                .Append("|");
            }
            return sb.ToString().TrimEnd(char.Parse("|"));
        }
    }
}
