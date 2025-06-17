using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public class VKeyboardReceived: UserControl
    {
        public delegate void WmCharEventHandler(string string_0);
        public delegate void WmWndProcEventHandler(int int_0, int int_1, int int_2);
        public delegate void KeyWndProcEventHandler(int int_0, int int_1, string string_0, bool bool_0, bool bool_1, bool bool_2);

        public event WmCharEventHandler wmCharEventHandler0;
        public event WmWndProcEventHandler WmWndProcEventHandler0;
        public event KeyWndProcEventHandler KeyWndProcEventHandler0;

		[DllImport("user32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		public static extern int GetAsyncKeyState(int int_10);
		public VKeyboardReceived()
        {
        }
		public void ProcessWndProc(int msg, int wParam, int lParam)
        {
			if (msg != 81)
			{
				switch (msg)
				{
					case 256:
					case 257:
					case 260:
					case 261:
						{
							Console.WriteLine("KeyboardHook: "+ (Keys)wParam);
							string text = Utils.smethod_19(checked((uint)wParam), (IntPtr)0, msg);
							if (text.Equals("{dk}", StringComparison.OrdinalIgnoreCase))
							{
								return;
							}
							bool bool_ = false;
							bool bool_2 = false;
							bool bool_3 = false;
							if (GetAsyncKeyState(17) != 0)
							{
								bool_ = true;
							}
							if (GetAsyncKeyState(164) != 0)
							{
								bool_2 = true;
							}
							if (GetAsyncKeyState(165) != 0)
							{
								bool_2 = true;
							}
							if (GetAsyncKeyState(91) != 0 || GetAsyncKeyState(92) != 0)
							{
								bool_3 = true;
							}
							KeyWndProcEventHandler keyWndProcEventHandler = KeyWndProcEventHandler0;
							if (keyWndProcEventHandler != null)
							{
								keyWndProcEventHandler(msg, Utils.smethod_16(wParam, lParam), text, bool_, bool_2, bool_3);
								return;
							}
							return;
						}
					case 258:
						{
							WmCharEventHandler wmCharEventHandler = wmCharEventHandler0;
							if (wmCharEventHandler != null)
							{
								wmCharEventHandler(char.ConvertFromUtf32(wParam));
								return;
							}
							return;
						}
				}
				if ((long)msg == 522L)
				{
					WmWndProcEventHandler wmWndProcEventHandler = WmWndProcEventHandler0;
					if (wmWndProcEventHandler != null)
					{
						wmWndProcEventHandler(msg, wParam, lParam);
					}
				}
				return;
			}
		}
		protected override void WndProc(ref Message m)
        {
			if (m.Msg == 7)
			{
				this.OnEnter(new EventArgs());
			}
			else if (m.Msg == 528 && (m.WParam.ToInt32() == 513 || m.WParam.ToInt32() == 516))
			{
				if (!base.ContainsFocus)
				{
					this.OnEnter(new EventArgs());
				}
			}
			else if (m.Msg == 2 && !base.IsDisposed && !base.Disposing)
			{
				base.Dispose();
			}
			else
			{
				if (m.Msg == 135)
				{
					base.WndProc(ref m);
					m.Result = new IntPtr(4);
					return;
				}
				this.ProcessWndProc(m.Msg, (int)m.WParam, (int)m.LParam);
			}
			base.WndProc(ref m);
		}
    }
}
