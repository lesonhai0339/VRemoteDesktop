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
        public TaskObject(RemoteType taskType, byte[] data, string receiveId = "", string receivePort = "")
        {
            TaskType = taskType;
            ReceiveId = receiveId;
            ReceivePort = receivePort;
            Data = data;
        }
        public TaskObject(RemoteType taskType, byte[] data, int length, string receiveId = "", string receivePort= "")
        {
            TaskType = taskType;
            ReceiveId = receiveId;
            ReceivePort = receivePort;
            Data = data;
            Length = length;
        }
        public TaskObject(RemoteType taskType, byte[] data, bool isSendHeader, string receiveId = "", string receivePort = "")
        {
            TaskType = taskType;
            ReceiveId = receiveId;
            ReceivePort = receivePort;
            Data = data;
            IsSendHeader = isSendHeader;
        }
        public TaskObject(RemoteType taskType, byte[] data, int length, bool isSendHeader, string receiveId = "", string receivePort = "")
        {
            TaskType = taskType;
            ReceiveId = receiveId;
            ReceivePort = receivePort;
            Data = data;
            Length = length;
            IsSendHeader = isSendHeader;
        }

        public RemoteType TaskType { get; set; }
        public string ReceiveId { get; set; } = "";
        public string ReceivePort { get; set; } = "";
        public byte[] Data { get; set; }
        public int Length { get; set; } = 0;
        public bool IsSendHeader { get; set; } = true;
    }
}
