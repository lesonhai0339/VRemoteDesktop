using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    public class SocketConnection : IDisposable
    {
        private bool isDisposed = false;
        private Socket _sck;
        private Action<IAsyncResult> _callback;
        private WindowsScreen _screen;
        public SocketConnection(Socket sck,Action<IAsyncResult> callback)
        {
            _sck = sck;
            _callback = callback;
            _screen = new WindowsScreen();
        }
        public void Connect(IPEndPoint endpoint)
        {
            try
            {
                if (_sck == null)
                {
                    _sck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                }
                _sck.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _sck.BeginConnect(endpoint, new AsyncCallback(_callback), _sck);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data received Error: ", ex.Message);
            }
            finally
            {
                _sck.Close();
            }
        }
        public void ShareScreen()
        {
            while (!isDisposed)
            {
                byte[] screenData= _screen.GrabDesktop();
                byte[] buffer= new byte[screenData.Length + 1];
                buffer[0] = 1;
                Buffer.BlockCopy(screenData, 0, buffer, 1, screenData.Length);
                _sck.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, new AsyncCallback(_callback), _sck);
            }
        }
        public void Dispose()
        {
            if(!isDisposed)
            {
                _sck?.Close();
                _sck = null;
                isDisposed = true;
            }
        }

       
    }
}
