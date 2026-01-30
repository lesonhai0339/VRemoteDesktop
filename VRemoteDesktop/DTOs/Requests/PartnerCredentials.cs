using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.DTOs.Requests
{
    public class PartnerCredentials
    {
        public PartnerCredentials(string id, string password)
        {
            Id = id;
            Password = password;
        }

        public string Id { get; private set; }
        public string Password { get; private set; }
    }
}
