using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.DTOs
{
    public class EventRegistration
    {
        public EventRegistration(Control control, string eventName, Delegate handler)
        {
            Control = control;
            EventName = eventName;
            Handler = handler;
        }

        public Control Control { get; set; }
        public string EventName { get; set; }
        public Delegate Handler { get; set; }
    }
}
