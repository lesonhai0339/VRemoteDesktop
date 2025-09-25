using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.RandomLength;
using VRemoteServer.Models;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.ConnectionManager
{
    public interface IClientInfoManager
    {
        ClientInfo GetMyInfo();
        string GetLocalIPAddress();
        void UpdateMyInfo(byte[] data);
        void UpdateMyInfo(ClientInfo info);
        bool IsAuthenticated(byte[] bytes, out ClientInfo clientInfo, out string connectionId);
    }
    public class ClientInfoManager: IClientInfoManager
    {
        private readonly object _lock = new object();
        private ClientInfo _me;
        public ClientInfoManager()
        {
            Me = InitMyInfo();
        }
        #region Properties
        public ClientInfo Me
        {
            get
            {
                lock (_lock)
                {
                    return _me;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _me = value;
                }
            }
        }
        #endregion
        #region Methods
        public ClientInfo GetMyInfo()
        {
            return Me;  
        }
        private ClientInfo InitMyInfo()
        {
            var computerName = Environment.MachineName;
            int width = Screen.PrimaryScreen.Bounds.Width;
            int height = Screen.PrimaryScreen.Bounds.Height;
            OperatingSystem os = Environment.OSVersion;
            ClientInfo info = new ClientInfo
            {
                Id = StringHelper.RandomStringNumber(ID_LENGTH),
                Password = StringHelper.RandomStringNumber(PASSWORD_LENGTH),
                ComputerName = computerName,
                Width = width,
                Height = height,
                MajorVersion = os.Version.Major.ToString(),
                MinorVersion = os.Version.Minor.ToString(),
                Ip = GetLocalIPAddress(),
                Port = AppSettingHelper.GetValue("LoginPort"),
                PublicIP = null
            };
            return info;
        }
        public string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            return "";
        }
        public void UpdateMyInfo(byte[] data)
        {
            string rawInfo = ByteArrayHelper.ConvertByteArrayToString(data, Enums.EncodingType.ASCII).GetResult();
            string[] info = StringHelper.StringToStringArrayWithSeparator(DefaultValue.DEFAULT_SEPARATOR);
            lock (_lock)
            {
                _me.TryParseData(info);
            }
        }
        public void UpdateMyInfo(ClientInfo info)
        {
            lock (_lock)
            {
                _me.Id = info.Id ?? Me.Id;
                _me.Password = info.Password ?? _me.Password;
                _me.ComputerName = info.ComputerName ?? _me.ComputerName;
                _me.Width = (info.Width != 0) ? info.Width : _me.Width;
                _me.Height = (info.Height != 0) ? info.Height : _me.Height;
                _me.MajorVersion = info.MajorVersion ?? _me.MajorVersion;
                _me.MinorVersion = info.MinorVersion ?? _me.MinorVersion;
                _me.Ip = info.Ip ?? _me.Ip;
                _me.PublicIP = info.PublicIP ?? _me.PublicIP;
                _me.Port = info.Port ?? _me.Port;
            }
        }
        public bool IsAuthenticated(byte[] bytes, out ClientInfo clientInfo, out string connectionId)
        {
            int indexAddedIncludesPartnerInfo = 3; //ConnectionId, my Id, my Password received from partner
            clientInfo = null;
            connectionId = null;

            string dataString = ByteArrayHelper.ConvertByteArrayToString(bytes, Enums.EncodingType.ASCII).GetResult();
            if (string.IsNullOrEmpty(dataString))
                return false;

            string[] data = StringHelper.StringToStringArrayWithSeparator(dataString);
            if (data.Length != DefaultClientInfo.CLIENT_INFO_MIN_FIELDS + indexAddedIncludesPartnerInfo)
                return false;

            bool isIdAndPasswordCorrect = string.Compare(data[1], Me.Id) == 0 && string.Compare(data[2], Me.Password) == 0;
            if (!isIdAndPasswordCorrect)
                return false;

            connectionId = data[0];
            //Note: data at index[0,1,2] are ConnectionId, MyId and MyPassword then partner info start at DefaultClientInfo.field + 3 instead DefaultClientInfo.field
            clientInfo = new ClientInfo
            {
                Id = data[DefaultClientInfo.CLIENT_INFO_ID_INDEX + indexAddedIncludesPartnerInfo],
                Password = data[DefaultClientInfo.CLIENT_INFO_PASSWORD_INDEX + indexAddedIncludesPartnerInfo],
                ComputerName = data[DefaultClientInfo.CLIENT_INFO_COMPUTER_NAME_INDEX + indexAddedIncludesPartnerInfo],
                Width = int.Parse(data[DefaultClientInfo.CLIENT_INFO_WIDTH_INDEX + indexAddedIncludesPartnerInfo]),
                Height = int.Parse(data[DefaultClientInfo.CLIENT_INFO_HEIGHT_INDEX + indexAddedIncludesPartnerInfo]),
                MajorVersion = data[DefaultClientInfo.CLIENT_INFO_MAJOR_VERSION_INDEX + indexAddedIncludesPartnerInfo],
                MinorVersion = data[DefaultClientInfo.CLIENT_INFO_MINOR_VERSION_INDEX + indexAddedIncludesPartnerInfo],
                Ip = data[DefaultClientInfo.CLIENT_INFO_IP_INDEX + indexAddedIncludesPartnerInfo],
                Port = data[DefaultClientInfo.CLIENT_INFO_PORT_INDEX + indexAddedIncludesPartnerInfo],
                PublicIP = data[DefaultClientInfo.CLIENT_INFO_PUBLIC_IP_INDEX + indexAddedIncludesPartnerInfo],
                //Id = data[3],
                //Password = data[4],
                //ComputerName = data[5],
                //Width = int.TryParse(data[6], out int width) ? width : 0,
                //Height = int.TryParse(data[7], out int height) ? height : 0,
                //MajorVersion = data[8],
                //MinorVersion = data[9],
                //Ip = data[10],
                //Port = data[11],
                //PublicIP = data[12],
            };

            return true;
        }
        #endregion
    }
}
