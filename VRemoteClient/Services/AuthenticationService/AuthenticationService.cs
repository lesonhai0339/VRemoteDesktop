using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Entities;
using VRemoteClient.Services.ConnectionService;

namespace VRemoteClient.Services.AuthenticationService
{
    public static class AuthenticationService
    {
        public static bool IsAuthenticated(string id, string password)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(password))
                return false;

            ClientInfo me = ConnectionManagerment.Me;
            return me.Id == id && me.Password == password;
        }
    }
}
