using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Management;
using System.IO;
using System.Text.RegularExpressions;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Services.Machine.DTOs;

namespace VRemoteDesktop.Services.Client
{
    public interface IMachineProfile
    {
        MachineInfo MachineInfo { get; }
        void UpdatePublicIp(string publicIp);
        void UpdatePort(string port);
        void UpdateLocalIp(string localIp);
        bool SameNetwork(string publicIp);
        Rectangle Bounds { get; }
    }
    public class MachineProfile : IMachineProfile, IDisposable
    {
        private const int defaultPort = 2399;
        private const string defaultFolder = "Vinhhy_Remote";
        private const string defaultPasswordFileName = "ps.txt";

        public MachineInfo _machineInfo;
        public Rectangle Bounds => GetBounds();
        public MachineProfile()
        {
            _machineInfo = InitializeMachineInfo();
        }
        private MachineInfo InitializeMachineInfo()
        {
            string signature = GetDiskSn(Application.StartupPath);
            string machineId = CreateMachineId(signature);

            string tempPassword = StringHelper.RandomStringNumber(4);

            string defaultPassword = GetDefaultPassword();

            string computerName = MachineName();

            var bounds = GetBounds();
            var width = bounds.Width;
            var height = bounds.Height;

            var osVersion = OsVersion();
            var major = osVersion.Version.Major;
            var minor = osVersion.Version.Minor;

            var ip = LocalIpAddress();

            var machineInfo = new MachineInfo(
                id: machineId,
                password: tempPassword,
                defaultPassword: defaultPassword,
                computerName: computerName,
                width: width,
                height: height, 
                majorVersion: major.ToString(),
                minorVersion: minor.ToString(),
                ip: ip.ToString(),
                publicIP: "",
                port: defaultPort.ToString() 
            );
            return machineInfo;
        }
        public bool SameNetwork(string publicIp)
        {
            return _machineInfo.PublicIP.Equals(publicIp, StringComparison.OrdinalIgnoreCase);
        }
        public bool Authentication(string id, string password, string defaultPassword)
        {
            return id.Equals(_machineInfo.Id) &&
                (password.Equals(_machineInfo.Password) || defaultPassword.Equals(_machineInfo.DefaultPassword));
        }
        public MachineInfo MachineInfo => _machineInfo;
        public void UpdatePublicIp(string publicIp)
        {
            _machineInfo.UpdatePublicIp(publicIp);
        }
        public void UpdatePort(string port)
        {
            _machineInfo.UpdatePort(port);
        }
        public void UpdateLocalIp(string localIp)
        {
            _machineInfo.UpdateLocalIp(localIp);
        }
        public void SetDefaultPassword(string password)
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), defaultFolder, defaultPasswordFileName);

            var encryptedData = Crypto.EncryptString(password);

            FileHelper.WriteToFile(data: encryptedData, filePath: filePath, append: true);
        }
        public string GetDefaultPassword()
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), defaultFolder, defaultPasswordFileName);

            var encryptedPassword = FileHelper.ReadFromFile(filePath).ToArray();
            if(encryptedPassword.Length > 0)
            {
                return Crypto.DecryptString(encryptedPassword.First());
            }
            else
            {
                return "";
            }
        }
        public OperatingSystem OsVersion()
        {
            return Environment.OSVersion;
        }
        public string MachineName()
        {
            return Environment.MachineName;
        }
        public Rectangle GetBounds()
        {
            return Screen.PrimaryScreen.Bounds;
        }
        public IPAddress LocalIpAddress()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address;
            }
        }
        public string CreateMachineId(string signature)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(signature));

                var digits = hash.Select(x => (x % 10).ToString()).Take(9);

                return string.Concat(digits);
            }
        }
        public static string GetDiskSn(string path)
        {
            try
            {
                string drive = GetDrive(path);
                if (drive != null)
                {
                    if (DriveExists(drive))
                    {
                        string sn = GetVolumeSerialNumber(drive.Substring(0, 1));

                        ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE VolumeSerialNumber='" + sn.Trim() + "'");

                        foreach (ManagementObject disk in searcher.Get())
                        {

                            string deviceID = disk["DeviceID"].ToString();
                            try
                            {

                                ManagementObjectSearcher physicalDiskSearcher =
                                    new ManagementObjectSearcher("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + deviceID + "'} WHERE ResultClass = Win32_DiskPartition");

                                ManagementObjectCollection processes = physicalDiskSearcher.Get();

                                if (processes.Count > 0)
                                {
                                    System.Management.ManagementObjectCollection.ManagementObjectEnumerator enumerator = processes.GetEnumerator();
                                    enumerator.MoveNext();
                                    ManagementObject firstProcess = (ManagementObject)enumerator.Current;

                                    string deviceID2 = firstProcess["DeviceID"].ToString();
                                    ManagementObjectSearcher searcherDiskDriveToDiskPartition = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDriveToDiskPartition");

                                    foreach (ManagementObject mo in searcherDiskDriveToDiskPartition.Get())
                                    {

                                        if (mo["Dependent"].ToString().Contains(deviceID2))
                                        {
                                            string physicalDrvDID = (string)mo["Antecedent"];
                                            try
                                            {
                                                var m = Regex.Match(physicalDrvDID, "DeviceID=\"(.*?)\"");
                                                if (m.Success)
                                                {
                                                    physicalDrvDID = m.Groups[1].Value;
                                                    physicalDrvDID = physicalDrvDID.Replace("\\\\", "\\");
                                                    ManagementObjectSearcher searcherDiskDrive = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive WHERE InterfaceType != 'USB'");

                                                    foreach (ManagementObject mo2 in searcherDiskDrive.Get())
                                                    {
                                                        if (mo2["DeviceID"].ToString().Contains(physicalDrvDID))
                                                        {
                                                            try
                                                            {

                                                                foreach (PropertyData p in mo2.Properties)
                                                                {
                                                                    Console.WriteLine(p.Name + ": " + p.Value);
                                                                }
                                                                return mo2["SerialNumber"].ToString().Trim();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                Console.WriteLine("Loi lay serial o cung {0} ", physicalDrvDID);
                                                                Console.WriteLine(ex.Message);
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("Lay thong tin {0} bi loi: ", physicalDrvDID);
                                                Console.WriteLine(ex.Message);
                                            }

                                        }
                                    }
                                }
                                //foreach (ManagementObject physicalDisk in physicalDiskSearcher.Get())
                                //{
                                //    // Retrieve physical disk information
                                //    foreach (PropertyData p in physicalDisk.Properties)
                                //    {
                                //        Console.WriteLine(p.Name + ": " + p.Value);
                                //    }
                                //}
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Loi khi dang kiem tra:{0} ", deviceID);
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("Khong tim duoc duong dan.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Loi GetDiskSn " + ex.Message + "::" + ex.StackTrace);

            }
            return null;
        }
        static string GetDrive(string path)
        {
            try
            {
                string drive = Path.GetPathRoot(path);
                return drive;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }
        }
        static string GetVolumeSerialNumber(string driveLetter)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT VolumeSerialNumber FROM Win32_LogicalDisk WHERE DeviceID = '" + driveLetter + ":'");
            ManagementObjectCollection collection = searcher.Get();

            foreach (ManagementObject obj in collection)
            {
                return obj["VolumeSerialNumber"].ToString();
            }

            throw new ArgumentException("Drive " + driveLetter + "not found.");
        }
        static bool DriveExists(string drivePath)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.Name.Equals(drivePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
