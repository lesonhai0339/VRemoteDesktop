using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vinhhy_Remote_Desktop_Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ConnectionManager con = new ConnectionManager();
            await con.Listen();
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);
            Console.WriteLine($"Available threads: {workerThreads}");
        }
    }
}
