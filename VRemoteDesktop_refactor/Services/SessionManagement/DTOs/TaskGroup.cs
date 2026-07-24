using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs
{
    public class TaskGroup
    {
        public TaskGroup(List<TaskObject> tasks)
        {
            Tasks = tasks;
        }
        public TaskGroup(TaskObject[] tasks)
        {
            Tasks = tasks.ToList();
        }

        public List<TaskObject> Tasks { get; set; }
    }
}
