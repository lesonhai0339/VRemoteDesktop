using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.DTOs;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomTableLayout: CustomUserControl
    {
        private TableLayoutPanel _table;
        private string _connectionId;
        public Action<string, object, EventArgs> EventHandlerAction { get; set; }
        public CustomTableLayout(string connectionId, Action<string, object, EventArgs> eventHandlerAction)
        {
            InitializeComponent();
            _connectionId = connectionId;
            EventHandlerAction = eventHandlerAction;
        }

        public TableLayoutPanel Table => _table;

        //Set number of row and column for TableLayoutPanel
        public CustomTableLayout SetColAndRow(int cols, int rows)
        {
            if (cols <= 0 || rows <= 0)
                throw new ArgumentException("Number of columns and rows must be greater than zero.");

            this._table.ColumnCount = cols;
            this._table.RowCount = rows;

            return this;
        }
        //Set style by property name and value
        public CustomTableLayout SetStyle(string propertyName, object value)
        {
            var e = this._table.GetType().GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);

            if(e == null)
                throw new ArgumentException($"Property '{propertyName}' not found on {_table.GetType().Name}.");

            if (!e.CanWrite)
                throw new InvalidOperationException($"Property '{propertyName}' is read-only on {_table.GetType().Name}.");

            if(value != null && !e.PropertyType.IsAssignableFrom(value.GetType()))
                throw new ArgumentException($"Value type '{value.GetType()}' is not compatible with property '{propertyName}' of type '{e.PropertyType}'.");

            e.SetValue(this._table, value, null);
            return this;
        }
        //set style for this class in bulk
        public CustomTableLayout SetStyles(params UIPropertyRegistration[] properties)
        {
            foreach(var property in properties)
            {
                SetStyle(property.PropertyName, property.Value);
            }
            return this;
        }
        //Set style for each column and row
        public CustomTableLayout SetColumAndRowStyle(List<ColumnStyle> colStyles, List<RowStyle> rowStyles)
        {
            if (colStyles == null) 
                throw new ArgumentNullException(nameof(colStyles));

            if (rowStyles == null) 
                throw new ArgumentNullException(nameof(rowStyles));

            if (colStyles.Count != _table.ColumnCount || rowStyles.Count != _table.RowCount)
                throw new InvalidOperationException("Number of styles not same with columns or rows");

            if (_table.ColumnStyles.Count > 0 || _table.RowStyles.Count > 0)
                ClearStyle();

            for (int i = 0; i < colStyles.Count; i++)
            {
                this._table.ColumnStyles.Add(colStyles[i]);
            }
            for(int i = 0; i< rowStyles.Count; i++)
            {
                this._table.RowStyles.Add(rowStyles[i]);
            }
            return this;
        }
        //Dynamic register event
        public CustomTableLayout RegisterEvent<T>(Control control, string eventName, T handler) where T : Delegate
        {
            var e = control.GetType().GetEvent(eventName);

            if(e == null)
                throw new ArgumentException($"Event '{eventName}' not found on control '{control.Name}'.");

            if(!e.EventHandlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException($"Handler type '{handler.GetType()}' is not compatible with event '{eventName}' on control '{control.Name}'.");

            e.AddEventHandler(control, handler);
            return this;
        }
        //Dynamic unregister event
        public CustomTableLayout UnRegisterEvent<T>(Control control, string eventName, T handler) where T: Delegate
        {
            var e = control.GetType().GetEvent(eventName);

            if (e == null)
                throw new ArgumentException(
                    $"Event '{eventName}' not found on control '{control.Name}'.");

            if (!e.EventHandlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException(
                    $"Handler type '{handler.GetType()}' is not compatible with event '{eventName}' on control '{control.Name}'.");

            e.RemoveEventHandler(control, handler);
            return this;
        }
        //Register events in bulk
        public CustomTableLayout RegisterEvents(params EventRegistration[] events)
        {
            foreach (var e in events)
            {
                RegisterEvent(e.Control, e.EventName, e.Handler);
            }
            return this;
        }
        //UnRegister events in bulk
        public CustomTableLayout UnRegisterEvents(params EventRegistration[] events)
        {
            foreach (var e in events)
            {
                UnRegisterEvent(e.Control, e.EventName, e.Handler);
            }
            return this;
        }
        //Event handler for custom events. call invoke to the action with connectionId
        public void EventHandler(object sender, EventArgs e)
        {
            if (EventHandlerAction != null)
            {
                EventHandlerAction(_connectionId,sender, e);
            }
        }
        //Add control to TableLayoutPanel with specific column and row index
        public void AddControl(string controlName, Control control, int colIndex, int rowIndex, bool isSetRowSpan = false)
        {
            if (control == null) 
                throw new ArgumentNullException(nameof(control));

            if (string.IsNullOrEmpty(controlName))
                throw new ArgumentException("Control name cannot be null or empty");

            if (colIndex < 0 || colIndex >= _table.ColumnCount)
                throw new ArgumentOutOfRangeException(nameof(colIndex));

            if (rowIndex < 0 || rowIndex >= _table.RowCount)
                throw new ArgumentOutOfRangeException(nameof(rowIndex));

            if (this.InvokeRequired)
            {
                this.Invoke(new Action<string, Control, int, int, bool>(AddControl), controlName, control, colIndex, rowIndex, isSetRowSpan);
                return;
            }

            base.AddControl(controlName, control);
            this._table.Controls.Add(control, colIndex, rowIndex);
            if (isSetRowSpan)
            {
                this._table.SetRowSpan(control, 2);
            }
        }
        //Clear colum and row style of TableLayoutPanel
        public CustomTableLayout ClearStyle()
        {
            this._table.ColumnStyles.Clear();
            this._table.RowStyles.Clear();
            return this;
        }

        private void InitializeComponent() 
        {
            this._table = new System.Windows.Forms.TableLayoutPanel();
            this.SuspendLayout();
            // 
            // _table
            // 
            this._table.AutoSize = true;
            this._table.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._table.Dock = System.Windows.Forms.DockStyle.Fill;
            this._table.Location = new System.Drawing.Point(0, 0);
            this._table.Name = "_table";
            this._table.Size = new System.Drawing.Size(331, 100);
            this._table.TabIndex = 0;
            // 
            // CustomTableLayout
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.Name = "CustomTableLayout";
            this.Size = new System.Drawing.Size(100, 100);
            this.Load += new System.EventHandler(this.CustomTableLayout_Load);
            this.ResumeLayout(false);

        }

        private void CustomTableLayout_Load(object sender, EventArgs e)
        {

        }
    }
}
