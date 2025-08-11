using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomTableLayout: CustomUserControl
    {
        private TableLayoutPanel _table;
        public CustomTableLayout()
        {
            InitializeComponent();
        }
        public CustomTableLayout SetColAndRow(int cols, int rows)
        {
            if (cols <= 0 || rows <= 0)
                throw new ArgumentException("Number of columns and rows must be greater than zero.");

            this._table.ColumnCount = cols;
            this._table.RowCount = rows;

            return this;
        }
        public CustomTableLayout SetStyle(List<ColumnStyle> colStyles, List<RowStyle> rowStyles)
        {
            if (colStyles.Count != _table.ColumnCount || rowStyles.Count != _table.RowCount)
                throw new Exception("Number of styles not same with columns or rows");

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
        public void RegisterEvent(Control control, string eventName, Delegate handler)
        {
            var e = control.GetType().GetEvent(eventName);

            if(e == null)
                throw new ArgumentException($"Event '{eventName}' not found on control '{control.Name}'.");

            if(!e.EventHandlerType.IsAssignableFrom(handler.GetType()))
                throw new ArgumentException($"Handler type '{handler.GetType()}' is not compatible with event '{eventName}' on control '{control.Name}'.");

            e.AddEventHandler(control, handler);
        }
        public void EventHandler(object sender, EventArgs e)
        {
            //Todo: handler event
        }
        public void AddControl(string controlName, Control control, int colIndex, int rowIndex)
        {
            base.AddControl(controlName, control);
            this._table.Controls.Add(control, colIndex, rowIndex);
        }
        public CustomTableLayout ClearStyle()
        {
            this._table.ColumnStyles.Clear();
            this._table.RowStyles.Clear();
            return this;
        }

        private void InitializeComponent() 
        {
            _table = new TableLayoutPanel();
            this._table.Dock = DockStyle.Fill;
            this._table.AutoSize = true;
            this._table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this._table.Name = "CustomTableLayout";
        }
    }
}
