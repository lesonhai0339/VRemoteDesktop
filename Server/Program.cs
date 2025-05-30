using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
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
