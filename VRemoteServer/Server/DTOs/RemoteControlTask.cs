using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.DTOs
{
    public class RemoteControlTask
    {
        public RemoteControlTask(TaskCompletionSource<bool> completed)
        {
            Timeout = Environment.TickCount64;
            Completed = completed;
        }
        public RemoteControlTask(long timeout, TaskCompletionSource<bool> completed)
        {
            Timeout = timeout;
            Completed = completed;
        }
        public long Timeout { get;private set; }
        public TaskCompletionSource<bool> Completed { get; private set; }   
    }
}
