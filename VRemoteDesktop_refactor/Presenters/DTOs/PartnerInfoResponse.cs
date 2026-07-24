using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.DTOs
{
    public class PartnerInfoResponse
    {
        public PartnerInfoResponse(string message)
        {
            Message = message;
        }
        public string Message { get; set; }
    }
}
