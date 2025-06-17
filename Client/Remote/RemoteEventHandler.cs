using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    class RemoteEventHandler
    {
        private TCPClient _client;
        private RemoteData _remoteData;
        public RemoteEventHandler(TCPClient client, RemoteData remoteData) 
        {
            _client = client;
            _remoteData = remoteData;
        }
        public void KeyDownEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine($"Down: {e.KeyCode} - {e.Modifiers}");
        }
        public void KeyUpEventHandler(object sender, KeyEventArgs e)
        {
            Console.WriteLine($"Up: {e.KeyCode} - {e.Modifiers}");
        }
        public void MouseMoveEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse move: X:{e.X} - Y:{e.Y}");
        }
        public void MouseClickEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse Click: X:{e.Delta} - Y:{e.Y}");
        }
        public void MouseWheelEventHandler(object sender, MouseEventArgs e)
        {
            Console.WriteLine($"Mouse Wheel: {e.Location}");
        }
        private void ProcessMouse(MouseEventArgs e)
        {
            string data = new StringBuilder()
                .Append("")
                .ToString();
        }
        private void ProcessKey(KeyEventArgs e)
        {
            if (e.Control)
            {

            }
            else if (e.Shift)
            {

            }
            else if (e.Alt)
            {

            }
        }
    }
}
