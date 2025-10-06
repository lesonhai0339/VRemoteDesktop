using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using VRemoteDesktop.Utils;

namespace VRemoteServer.Models
{
    [DataContract]
    public class ClientInfo
    {
        public ClientInfo() { }
        public ClientInfo(string id,
            string password,
            string computerName,
            int width,
            int height,
            string majorVersion,
            string minorVersion,
            string ip,
            string publicIP,
            string port)
        {
            Id = id;
            Password = password;
            ComputerName = computerName;
            Width = width;
            Height = height;
            MajorVersion = majorVersion;
            MinorVersion = minorVersion;
            Ip = ip;
            PublicIP = publicIP;
            Port = port;
        }
        [DataMember(Order = 0)]
        public string Id { get; set; }
        [DataMember(Order = 1)]
        public string Password { get; set; }
        [DataMember(Order = 2)]
        public string ComputerName { get; set; }
        [DataMember(Order = 3)]
        public int Width { get; set; }
        [DataMember(Order = 4)]
        public int Height { get; set; }
        [DataMember(Order = 5)]
        public string MajorVersion { get; set; }
        [DataMember(Order = 6)]
        public string MinorVersion { get; set; }
        [DataMember(Order = 7)]
        public string Ip { get; set; }
        [DataMember(Order = 8)]
        public string Port { get; set; }
        [DataMember(Order = 9)]
        public string PublicIP { get; set; }
        public string ToNetworkString()
        {
            StringBuilder sb = new StringBuilder();
            var props = this.GetType()
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute)) != null)
                            .Select(p => new { prop = p, attr = (DataMemberAttribute)Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute))})
                            .Where(p => p.attr != null)
                            .OrderBy(p => p.attr.Order)
                            .Select(p => p.prop)
                            .ToArray();

            foreach (var prop in props)
            {
                var value = prop.GetValue(this, null);
                sb.Append(value ?? "-1").Append(DefaultValue.DEFAULT_SEPARATOR);
            }
            return sb.ToString().TrimEnd(DefaultValue.DEFAULT_SEPARATOR.ToCharArray());
        }
        public bool TryParseData(string[] data)
        {
            var props = this.GetType()
                            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute)) != null)
                            .Select(p => new { prop = p, attr = (DataMemberAttribute)Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute)) })
                            .Where(p => p.attr != null)
                            .OrderBy(p => p.attr.Order)
                            .Select(p => p.prop)
                            .ToArray();

            if (data.Length != props.Length)
                return false;

            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                try
                {
                    object value;
                    if (string.IsNullOrEmpty(data[i]))
                    {
                        value = type.IsValueType ? Activator.CreateInstance(type) : null;
                    }
                    else
                    {
                        value = Convert.ChangeType(data[i], type, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    prop.SetValue(this, value, null);
                }
                catch
                {
                    return false;
                }
            }
            return true;
        }
    }
    //public class ClientInfo
    //{
    //    public ClientInfo() { }
    //    public ClientInfo(string id, string password, string computerName, int width, int height, string majorVersion, string minorVersion, string ip, string publicIP, string port)
    //    {
    //        Id = id;
    //        Password = password;
    //        ComputerName = computerName;
    //        Width = width;
    //        Height = height;
    //        MajorVersion = majorVersion;
    //        MinorVersion = minorVersion;
    //        Ip = ip;
    //        PublicIP = publicIP;
    //        Port = port;
    //    }

    //    public string Id { get; set; }
    //    public string Password { get; set; }
    //    public string ComputerName { get; set; }
    //    public int Width { get; set; }
    //    public int Height { get; set; }
    //    public string MajorVersion { get; set; }
    //    public string MinorVersion { get; set; }
    //    public string Ip { get; set; }
    //    public string Port { get; set; }
    //    public string PublicIP { get; set; }
    //    public string ToNetworkString()
    //    {
    //        return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion, Ip, Port, PublicIP);
    //    }
    //}
}
