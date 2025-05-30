using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ConnectionManager connectionManager = new ConnectionManager();
            await connectionManager.Listen();
        }
    }
}
