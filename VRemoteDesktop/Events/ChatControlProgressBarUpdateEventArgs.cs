using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Layouts;

namespace VRemoteDesktop.Events
{
    public class ChatControlProgressBarUpdateEventArgs: EventArgs
    {
        public ChatControlProgressBarUpdateEventArgs(FileReceivedLayout fileLayout, int num)
        {
            FileLayout = fileLayout;
            Num = num;
        }

        public FileReceivedLayout FileLayout { get; set; }
        public int Num { get; set; }
    }
}
