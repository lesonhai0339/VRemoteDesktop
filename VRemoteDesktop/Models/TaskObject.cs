using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Models
{
    public class TaskObject
    {
        public TaskObject()
        {
        }

        public DataType TaskType { get; set; } = DataType.None;
        public string SessionId { get; set; } = "0000000000000000";
        public byte[] Data { get; set; } = new byte[0];
        public int Length => Data?.Length ?? 0;
        public bool IsSendHeader { get; set; } = true;
    }
}
