using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Services.Machine.DTOs;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService
 {
        #region System
        public bool SetDefaultPassword(string password)
        {
            try
            {
                string historyFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), HeaderSchema.RootFolder, HeaderSchema.PasswordFile);

                var encryptedData = VCrypto.EncryptString(password);

                FileHelper.WriteToFile(data: encryptedData, filePath: historyFilePath, append: false);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public string GetDefaultPassword()
        {
            try
            {
                string defaultPasswordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), HeaderSchema.RootFolder, HeaderSchema.PasswordFile);

                var encryptedPassword = FileHelper.ReadFromFile2(defaultPasswordPath);

                if (encryptedPassword == null || encryptedPassword.Count() == 0)
                    return string.Empty;

                return VCrypto.DecryptString(encryptedPassword.First());
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "GetDefaultPassword error");
                return string.Empty;
            }
        }
        public MachineInfo GetMachineInfo()
     {
         return _machineProfile.MachineInfo; 
     }
     public void UpdatePublicIp(string publicIp)
     {
         _machineProfile.UpdatePublicIp(publicIp);
     }
     public void UpdateLocalIp(string localIp)
     {
         _machineProfile.UpdateLocalIp(localIp);
     }
     public void UpdatePort(string port)
     {
         _machineProfile.UpdatePort(port);
     }
     #endregion
 }
}
