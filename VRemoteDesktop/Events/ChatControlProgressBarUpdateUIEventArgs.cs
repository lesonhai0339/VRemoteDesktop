using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Layouts;

namespace VRemoteDesktop.Events
{
    public class ChatControlProgressBarUpdateUIEventArgs: EventArgs
    {
        public ChatControlProgressBarUpdateUIEventArgs(FileAttachmentLayout fileLayout, int num)
        {
            FileLayout = fileLayout;
            Num = num;
        }

        public FileAttachmentLayout FileLayout { get; set; }
        public int Num { get; set; }
    }
}
