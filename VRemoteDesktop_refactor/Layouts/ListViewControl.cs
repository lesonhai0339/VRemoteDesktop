using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Presenters.DTOs;

namespace Vsign4.VRemoteDesktop.Layouts
{
    public partial class ListViewControl : UserControl
    {
        private const int MaxDropHeight = 200;
        private bool flag = false;
        private ConnectHistoryInfo[] _infoArray;
        private ConnectHistoryInfo[] _matchedInfoArray;
        private TableLayoutPanel table;

        private TextBox txtInput;
        private Label btnToggle;

        private Panel pnlContainer;
        private FlowLayoutPanel fpnlList;
        private int _width;
        private int _height;
        private int _heightScale;
        public ListViewControl()
        {
            InitializeComponent();
            
        }
        public string InputText
        {
            get
            {
                return txtInput.Text;
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (table != null) return;
  
            InitializeComponents();
        }
        private void InitializeComponents()
        {
            this._width = this.Width;
            this._height = this.Height;
            this._heightScale = (int)(_height * 1.2);


            this.BorderStyle = BorderStyle.FixedSingle;
            this.Padding = Padding.Empty;
            this.Margin = Padding.Empty;
            this.BackColor = Color.White;

            table = new TableLayoutPanel
            {
                ColumnCount = 2,
                RowCount = 2,
                AutoSize = false,
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            };

            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 87f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 13f));

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, _height));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));

            txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9F),
                BorderStyle = BorderStyle.None,
            };
            txtInput.Click += (s,e)=>
            {
                if (flag)
                    ShowAndHideList();
            };
            txtInput.TextChanged += (s, e) =>
            {
                Search(txtInput.Text);
            };
            this.table.Controls.Add(txtInput, 0, 0);

            btnToggle = new Label
            {
                BackColor = Color.WhiteSmoke,
                Dock = DockStyle.Fill,
                Text = "▼",
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
            };
            btnToggle.MouseEnter += (o, e) => btnToggle.BackColor = Color.LightGray;
            btnToggle.MouseLeave += (o, e) => btnToggle.BackColor = Color.WhiteSmoke;
            btnToggle.Click += BtnToggle_Click;
            btnToggle.DoubleClick += BtnToggle_Click;

            this.table.Controls.Add(btnToggle, 1, 0);

            pnlContainer = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = false,
                Visible = false,
                Dock = DockStyle.Fill,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
            };

            //Let the FlowLayoutPanel itself own the scrolling (Dock=Fill + AutoScroll)
            //instead of an AutoSize panel nested in an AutoScroll one, which does not
            //reliably raise the vertical scrollbar.
            fpnlList = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = false,
                AutoScroll = true,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            //Keep items inside the visible width so the vertical scrollbar (when it
            //appears and shrinks the client area) never causes a horizontal scrollbar.
            fpnlList.ClientSizeChanged += (s, e) => ResizeItems();
            pnlContainer.Controls.Add(fpnlList);

            this.table.Controls.Add(pnlContainer, 0, 1);
            this.table.SetColumnSpan(pnlContainer, 2);

            this.Controls.Add(table);

        }
        private void Search(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                _matchedInfoArray = _infoArray;
            }
            else
            {
                _matchedInfoArray = _infoArray.Where(x => x.Id.Contains(keyword)).ToArray();
            }

            //Always show fpnList when txtInput.Textchanged
            if (!flag)
            {
                UpdateHistory(_matchedInfoArray, true);
            }
            else
            {
                flag = !flag;
                UpdateHistory(_matchedInfoArray, true);
            }
        }
        private void AddItem(ConnectHistoryInfo info)
        {

            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(info.LastConnect, out dto))
                dto = DateTimeOffset.UtcNow;

            Panel pn = new Panel
            {
                AutoSize = false,
                Width = fpnlList.ClientSize.Width - 2,
                Height = _heightScale,
                BorderStyle = BorderStyle.None,
                Padding = new Padding(1),
                Margin = new Padding(1),
                BackColor = Color.White,
                Cursor = Cursors.Hand,    
                Tag = info.Name
            };
            Label idAndName = new Label
            {
                Name = "lb1",
                Tag = info,
                Text = string.Format("{0} - {1}", info.Id, info.Name),
                TextAlign = ContentAlignment.TopLeft,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = true,
            };
            Label connectDate = new Label
            {
                Name = "lb2",
                Text = dto.AddMinutes(420).ToString("HH:mm:ss - dd/MM/yyyy"), //add 420 minutes for VietName UTC+7
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 7F),
                Dock = DockStyle.Bottom,
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                AutoSize = true,
            };
            pn.Controls.Add(idAndName);
            pn.Controls.Add(connectDate);

            pn.MouseEnter += HoverEnter;
            pn.MouseLeave += HoverLeave;
            pn.Click += ClickEvent;

            foreach (Control c in pn.Controls)
            {
                c.MouseEnter += HoverEnter;
                c.MouseLeave += HoverLeave;
                c.Click += ClickEvent;
            }
            fpnlList.Controls.Add(pn);
        }
        private void ResizeItems()
        {
            if (fpnlList == null) return;

            int w = fpnlList.ClientSize.Width - 2;
            if (w <= 0) return;

            foreach (Control c in fpnlList.Controls)
            {
                if (c.Width != w)
                    c.Width = w;
            }
        }
        void HoverEnter(object sender, EventArgs e)
        {
            Panel pn = sender as Panel ?? ((Control)sender).Parent as Panel;
            if (pn == null) return;

            pn.BackColor = ColorTranslator.FromHtml("#CCFFFF");
            pn.BorderStyle = BorderStyle.FixedSingle;
        }

        void HoverLeave(object sender, EventArgs e)
        {
            Panel pn = sender as Panel ?? ((Control)sender).Parent as Panel;
            if (pn == null) return;

            pn.BackColor = Color.White;
            pn.BorderStyle = BorderStyle.None;
        }
        void ClickEvent(object sender, EventArgs e)
        {
            Panel pn = sender as Panel ?? ((Control)sender).Parent as Panel;
            if (pn == null) return;

            foreach (Control c in pn.Controls)
            {
                if (string.Compare(c.Name, "lb1", true) == 0)
                {
                    txtInput.Text = ((ConnectHistoryInfo)c.Tag).Id;
                    ShowAndHideList();
                }
            }
        }
        private void BtnToggle_Click(object sender, EventArgs e)
        {
            Array.Clear(_matchedInfoArray, 0, _matchedInfoArray.Length);
            _matchedInfoArray = _infoArray.ToArray();
            UpdateHistory(_matchedInfoArray, flag);
        }
        private void UpdateHistory(IEnumerable<ConnectHistoryInfo> array, bool show)
        {
            fpnlList.SuspendLayout();
            fpnlList.Controls.Clear();
            foreach (var item in array)
            {
                AddItem(item);
            }

            ShowAndHideList();

            fpnlList.ResumeLayout();

        }
        void ShowAndHideList()
        {
            flag = !flag;
            pnlContainer.Visible = flag;
            btnToggle.Text = flag ? "▲" : "▼";

            //Measure the real content height instead of estimating from a magic
            //per-item value, so the drop-down is exactly as tall as it needs to be
            //(and AutoScroll kicks in once it exceeds MaxDropHeight).
            int content = 0;
            foreach (Control c in fpnlList.Controls)
                content += c.Height + c.Margin.Vertical;

            //+2 accounts for pnlContainer's FixedSingle border so items fit without
            //a scrollbar until content passes MaxDropHeight.
            int h = content == 0 ? 0 : Math.Min(content + 2, MaxDropHeight);

            table.RowStyles[0].SizeType = SizeType.Absolute;
            table.RowStyles[0].Height = _height;

            table.RowStyles[1].SizeType = SizeType.Absolute;
            table.RowStyles[1].Height = flag ? h : 0;

            this.Height = flag ? (_height + h) : _height;
        }
        public void LoadHistory(IEnumerable<ConnectHistoryInfo> data)
        {
            _infoArray = data
                .GroupBy(x => x.Id)
                .Select(x => new ConnectHistoryInfo
                {
                    Id = x.Key,
                    Name = x.First().Name,
                    LastConnect = x.Max(g => GetDay(g.LastConnect)).ToString()
                })
                .OrderByDescending(x => GetDay(x.LastConnect))
                .ToArray();

            _matchedInfoArray = _infoArray.ToArray();
            flag = !flag;
            UpdateHistory(_matchedInfoArray, flag);
        }
        private DateTimeOffset GetDay(string datetimeString)
        {
            DateTimeOffset datetime;
            if (DateTimeOffset.TryParse(datetimeString, out datetime))
            {
                return datetime;
            }
            return DateTimeOffset.MinValue;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle rect = ClientRectangle;
            rect.Width -= 1;
            rect.Height -= 1;

            ControlPaint.DrawBorder(
                e.Graphics,
                rect,
                Color.Aqua,
                ButtonBorderStyle.Solid
            );
        }
    }
}
