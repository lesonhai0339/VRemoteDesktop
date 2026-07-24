using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs
{
    public class QueueItem
    {
        public QueueItem(object data)
        {
            Data = data;
        }

        public object Data { get; set; }
    }
}
