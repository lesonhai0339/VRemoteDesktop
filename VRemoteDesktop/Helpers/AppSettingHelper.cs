using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Helpers
{
    public static class AppSettingHelper
    {
        public static void SetValueByKey(string key, string value)
        {
            bool existed = ConfigurationManager.AppSettings[key] != null;
            if (existed)
            {
                ConfigurationManager.AppSettings[key] = value;
            }
        }
        public static string Getvalue(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }
    }
}
