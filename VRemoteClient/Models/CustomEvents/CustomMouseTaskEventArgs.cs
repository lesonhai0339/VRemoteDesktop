using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Entities;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.CustomEvents
{
    public class CustomMouseTaskEventArgs: EventArgs
    {
        public TaskObject Task { get; set; }
    }
}
