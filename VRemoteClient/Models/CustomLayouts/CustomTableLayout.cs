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

        public TableLayoutPanel Table => _table;

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
            if(sender is Button btn)
            {
                if(btn.Name == "btnSave")
                {
                    MessageBox.Show("Save button clicked", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                if(btn.Name == "btnCancel")
                {
                    MessageBox.Show("Cancel button clicked", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
            }
            //Todo: handler event
        }
        public void AddControl(string controlName, Control control, int colIndex, int rowIndex, bool isSetRowSpan = false)
        {
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
            this.Size = new System.Drawing.Size(1426, 655);
            this.Load += new System.EventHandler(this.CustomTableLayout_Load);
            this.ResumeLayout(false);

        }

        private void CustomTableLayout_Load(object sender, EventArgs e)
        {

        }
    }
}
