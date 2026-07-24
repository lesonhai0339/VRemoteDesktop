using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Helpers
{
    public static class AppSettingHelper
    {
        //Lay key tu app.config
        public static void SetValueByKey(string key, string value)
        {
            bool existed = ConfigurationManager.AppSettings[key] != null;
            if (existed)
            {
                ConfigurationManager.AppSettings[key] = value;
            }
        }
        public static string GetValue(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }
    }
}
