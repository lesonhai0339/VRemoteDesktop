using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.Authentication
{
    internal static class Authentication
    {
        public static bool IsAuthenticated(string id, string password)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
                return false;

            Client me = ConnectionManager.ConnectionManager.Me;
            return me.Id == id && me.Password == password;
        }
    }
}
