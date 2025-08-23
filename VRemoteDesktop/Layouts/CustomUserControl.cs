using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class CustomUserControl: UserControl
    {
        private readonly object _lockObject = new object();
        protected ConcurrentDictionary<string, Control> _controlAdded;
        public CustomUserControl()
        {
            InitializeComponent();
            _controlAdded = new ConcurrentDictionary<string, Control>();
        }
        public virtual void AddControl(string controlName, Control control)
        {
            if(string.IsNullOrWhiteSpace(controlName))
                throw new ArgumentException("Control name cannot be null or empty.", nameof(controlName));

            if (!controlName.Equals(control.Name))
                //Re-assign name for the control 
                control.Name = controlName;

            if (_controlAdded.TryGetValue(controlName, out Control c))
            {
                throw new ArgumentException("Control with the same name is existed.", nameof(controlName));
            }

            _controlAdded.TryAdd(control.Name, control);
            this.Controls.Add(control);
        }
        public virtual T GetControl<T>(string controlName) where T : Control
        {
            return _controlAdded.TryGetValue(controlName, out Control control) ? control as T : null;
        }
        public virtual IEnumerable<T> GetControlsOfType<T>() where T : Control
        {
            return _controlAdded.Values.OfType<T>();
        }
        public virtual void RemoveControls(params string[] controlNames)
        {
            foreach (string name in controlNames)
                RemoveControl(name);
        }

        public virtual void RemoveControls(params Control[] controls)
        {
            foreach (Control control in controls)
                RemoveControl(control);
        }
        public virtual void RemoveControl(Control control)
        {
            lock (_lockObject)
            {
                try
                {
                    var existedControl = _controlAdded.FirstOrDefault(x => x.Value == control);
                    if (existedControl.Key != null && existedControl.Value != null)
                    {
                        this.Controls.Remove(existedControl.Value);
                        _controlAdded.TryRemove(existedControl.Key, out var _);

                        existedControl.Value.Dispose(); 
                    }
                }
                catch(Exception ex)
                {
                    throw new Exception("Error when remove control", ex);
                }
            }
        }
        public virtual void RemoveControl(string controlName)
        {
            lock (_lockObject)
            {
                try
                {
                    if(_controlAdded.TryGetValue(controlName, out var control))
                    {
                        this.Controls.Remove(control);
                        _controlAdded.TryRemove(controlName, out var _);

                        control.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error when remove control", ex);
                }
            }
        }
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // CustomUserControl
            // 
            this.Dock = DockStyle.Fill;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "CustomUserControl";
            this.Size = new System.Drawing.Size(100, 100);
            this.Load += new System.EventHandler(this.CustomUserControl_Load);
            this.ResumeLayout(false);

        }

        private void CustomUserControl_Load(object sender, EventArgs e)
        {

        }
    }
}
