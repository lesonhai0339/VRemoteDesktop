using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.Entities
{
    public class TaskGroup
    {
        public TaskGroup(List<TaskObject> tasks)
        {
            Tasks = tasks;
        }

        public List<TaskObject> Tasks { get; set; }
    }
    public class TaskObject
    {
        public TaskObject()
        {
        }

        public SocketDataType TaskType { get; set; } = SocketDataType.None;
        public string SessionId { get; set; } = "0000000000000000";
        public byte[] Data { get; set; }= new byte[0];
        public int Length => Data?.Length ?? 0;
        public bool IsSendHeader { get; set; } = true;
    }
}
