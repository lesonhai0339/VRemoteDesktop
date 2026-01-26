using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Presenters.Events
{
    public class MainDataEventArgs: EventArgs
    {
        public MainDataEventArgs(object data)
        {
            Data = data;
        }
            public object Data { get; set; }    
    }
}
