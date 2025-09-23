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
    //[DataContract]
    //public class ConnectionInfo
    //{
    //    public ConnectionInfo() { }
    //    public ConnectionInfo(string id,
    //        string password,
    //        string computerName,
    //        int width,
    //        int height,
    //        string majorVersion,
    //        string minorVersion,
    //        string ip,
    //        string publicIP,
    //        string port,
    //        SocketConnection socketConnection)
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
    //        SocketConnection = socketConnection;
    //    }
    //    [DataMember(Order = 0)]
    //    public string Id { get; set; }
    //    [DataMember(Order = 1)]
    //    public string Password { get; set; }
    //    [DataMember(Order = 2)]
    //    public string ComputerName { get; set; }
    //    [DataMember(Order = 3)]
    //    public int Width { get; set; }
    //    [DataMember(Order = 4)]
    //    public int Height { get; set; }
    //    [DataMember(Order = 5)]
    //    public string MajorVersion { get; set; }
    //    [DataMember(Order = 6)]
    //    public string MinorVersion { get; set; }
    //    [DataMember(Order = 7)]
    //    public string Ip { get; set; }
    //    [DataMember(Order = 8)]
    //    public string Port { get; set; }
    //    [DataMember(Order = 9)]
    //    public string PublicIP { get; set; }

    //    [NotMapped]
    //    public SocketConnection SocketConnection { get; set; }
    //    public string ToNetworkString()
    //    {
    //        StringBuilder sb = new StringBuilder();
    //        var props = this.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
    //            .Where(p => !Attribute.IsDefined(p, typeof(NotMappedAttribute)))
    //            .OrderBy(p =>
    //            {
    //                var attr = p.GetCustomAttribute<DataMemberAttribute>();
    //                return attr?.Order ?? int.MaxValue;
    //            });

    //        foreach (var prop in props)
    //        {
    //            if (Attribute.IsDefined(prop, typeof(NotMappedAttribute)))
    //                continue;

    //            var value = prop.GetValue(this);
    //            sb.Append(value ?? string.Empty).Append(DefaultValue.Common.SEPARATOR);
    //        }
    //        return sb.ToString().TrimEnd(DefaultValue.Common.SEPARATOR);
    //    }
    //    public bool TryParseData(string[] data)
    //    {
    //        var props = this.GetType()
    //                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    //                        .Where(p => !Attribute.IsDefined(p, typeof(NotMappedAttribute)))
    //                        .OrderBy(p =>
    //                        {
    //                            var attr = p.GetCustomAttribute<DataMemberAttribute>();
    //                            return attr?.Order ?? int.MaxValue;
    //                        })
    //                        .ToArray();

    //        if (data.Length != props.Length)
    //            return false;

    //        for (int i = 0; i < props.Length; i++)
    //        {
    //            var prop = props[i];
    //            var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

    //            try
    //            {
    //                object value;
    //                if (string.IsNullOrEmpty(data[i]))
    //                {
    //                    value = type.IsValueType ? Activator.CreateInstance(type) : null;
    //                }
    //                else
    //                {
    //                    value = Convert.ChangeType(data[i], type, System.Globalization.CultureInfo.InvariantCulture);
    //                }
    //                prop.SetValue(this, value);
    //            }
    //            catch
    //            {
    //                return false;
    //            }
    //        }
    //        return true;
    //    }
    //}
    public class ClientInfo
    {
        public ClientInfo() { }
        public ClientInfo(string id, string password, string computerName, int width, int height, string majorVersion, string minorVersion, string ip, string publicIP, string port)
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

        public string Id { get; set; }
        public string Password { get; set; }
        public string ComputerName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string MajorVersion { get; set; }
        public string MinorVersion { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }
        public string PublicIP { get; set; }
        public string ToNetworkString()
        {
            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}", Id, Password, ComputerName, Width, Height, MajorVersion, MinorVersion, Ip, Port, PublicIP);
        }
    }
}
