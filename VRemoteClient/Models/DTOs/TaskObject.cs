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
        public TaskObject(RemoteType taskType, byte[] data, string sessionId = "")
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
        }
        public TaskObject(RemoteType taskType, byte[] data, int length, string sessionId = "")
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
            Length = length;
        }
        public TaskObject(RemoteType taskType, byte[] data, bool isSendHeader, string sessionId = "")
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
            IsSendHeader = isSendHeader;
        }
        public TaskObject(RemoteType taskType, byte[] data, int length, bool isSendHeader, string sessionId = "")
        {
            TaskType = taskType;
            SessionId = sessionId;
            Data = data;
            Length = length;
            IsSendHeader = isSendHeader;
        }

        public RemoteType TaskType { get; set; }
        public string SessionId { get; set; } = "0000000000000000";
        public byte[] Data { get; set; }
        public int Length { get; set; } = 0;
        public bool IsSendHeader { get; set; } = true;
    }
}
