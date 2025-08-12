using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteClient.Models.DTOs
{
    public class UIPropertyRegistration
    {
        public UIPropertyRegistration(string propertyname, object value)
        {
            PropertyName = propertyname;
            Value = value;
        }
        public string PropertyName { get;set; }
        public object Value { get; set; }   
    }
}
