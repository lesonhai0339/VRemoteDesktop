using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.Entities
{
    public class KeyboardObject
    {
        public Keys Key { get; set; } = Keys.None;
        public bool IsKeyUp { get; set; } = false; 
        public List<Keys> Modifiers { get;set; }= new List<Keys>();
        public int ModifiersReleased { get; set; } = 0; 
    }
}
