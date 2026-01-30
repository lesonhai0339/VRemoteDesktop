using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.SessionManagement.Events.ClientSession
{
    public enum ScreenType
    {
        FullScreen,
        DirtyRegions
    }
    public class ClientSessionScreenReceivedEventArgs: EventArgs
    {
        public ClientSessionScreenReceivedEventArgs(ScreenType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

            public ScreenType Type { get; private set; }
        public byte[] Data { get; set; }
    }
}
