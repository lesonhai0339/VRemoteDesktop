using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using VRemoteServer.RelayServer.Helpers;

namespace VRemoteServer.RelayServer.DTOs
{
    public abstract class BaseClass
    {
        public string ToNetworkString()
        {
            StringBuilder sb = new StringBuilder();

            var props = this.GetType()
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute)) != null)
                             .Select(p => new { prop = p, attr = (DataMemberAttribute)Attribute.GetCustomAttribute(p, typeof(DataMemberAttribute)) })
                             .Where(p => p.attr != null)
                             .OrderBy(p => p.attr.Order)
                             .Select(p => p.prop)
                             .ToArray();

            foreach (var prop in props)
            {
                var value = prop.GetValue(this, null);
                sb.Append(value ?? "-1").Append(DefaultValue.Common.SEPARATOR);
            }
            return sb.ToString().TrimEnd(DefaultValue.Common.SEPARATOR);
        }
        public bool TryParseDataWithSeparator(byte[] raw, Encoding encoding, char separator = '|')
        {
            string[] infoArray =  encoding.ByteArrayToStringWithSeparator(raw, separator);

            if (infoArray.Length <= 0) 
                return false;

            return TryParseData(infoArray);
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
}
