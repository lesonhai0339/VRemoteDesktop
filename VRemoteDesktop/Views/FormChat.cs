using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.ViewModels;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private ChatViewModel _chatViewModel;
        private string _filePath;
        public FormChat()
        {
            InitializeComponent();
            ChatView = new ChatViewModel();
            SetupBinding();
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;

            this.Location = new Point(
                workingArea.Right - this.Width,
                workingArea.Bottom - this.Height - 120
            );
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;

            fpnChat.FlowDirection = FlowDirection.TopDown;
            fpnChat.WrapContents = false;
            fpnChat.AutoScroll = true;
            fpnChat.BorderStyle = BorderStyle.FixedSingle;
            fpnChat.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);
            fpnChat.BackColor = Color.White;


            fpnNumberChatConnection.BorderStyle = BorderStyle.FixedSingle;
            fpnNumberChatConnection.FlowDirection = FlowDirection.TopDown;
            fpnNumberChatConnection.WrapContents = false;
            fpnNumberChatConnection.BackColor = Color.Yellow;
            fpnNumberChatConnection.AutoScroll = true;
        }
        public ChatViewModel ChatView
        {
            get => _chatViewModel;
            private set => _chatViewModel = value;
        }
        private void SetupBinding()
        {
            _chatViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _chatViewModel.ControlEvent += ControlEventHandler;
            _chatViewModel.TestEvent += TestEventHandler;
        }

        private void TestEventHandler(Control control, int num)
        {

            if(control is FileReceivedLayout f)
            {
                UpdateBar(f, num);
            }
        }
        private void UpdateBar(FileReceivedLayout f, int num)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<FileReceivedLayout, int>(UpdateBar), f, num);
                return;
            }
            f.UpdateProgressBar(num);
        }

        private void ControlEventHandler(Control control)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Control>(ControlEventHandler), control);
                return;
            }
            control.MaximumSize = new Size(fpnChat.Width - SystemInformation.VerticalScrollBarWidth - 10, 0);
            fpnChat.Controls.Add(control);
            fpnChat.SetFlowBreak(control, true);

            fpnChat.PerformLayout();
            fpnChat.Refresh();
            fpnChat.Invalidate();

            fpnChat.ScrollControlIntoView(control);
        }

        private void Add(int width)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<int>(Add), width);
                return;
            }
            var value = _chatViewModel.ClientAdded;

            var control = _chatViewModel.NewControl(value, width);
            fpnNumberChatConnection.Controls.Add(control);

            fpnNumberChatConnection.PerformLayout();
            fpnNumberChatConnection.Refresh();
            fpnNumberChatConnection.Invalidate();

            fpnNumberChatConnection.ScrollControlIntoView(control);
        }
        private void Remove()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Remove));
                return;
            }
            var value = _chatViewModel.ClientRemoved;

            var lbs = fpnNumberChatConnection.Controls.Find(value, true);

            foreach (var lb in lbs)
            {
                lb.Click -= _chatViewModel.EventCallback;
                fpnNumberChatConnection.Controls.Remove(lb);
            }
        }
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(_chatViewModel.ClientAdded))
            {
                Add(fpnNumberChatConnection.Width);
            }
            if (e.PropertyName == nameof(_chatViewModel.ClientRemoved))
            {
                Remove();
            }
        }
        private void FormChat_Load(object sender, EventArgs e)
        {
        }

        private void fpnNumberChatConnection_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string text = txtChatContent.Text;
            _chatViewModel.SendChatMessage(text);
        }

        private void btnSendAttachment_Click(object sender, EventArgs e)
        {
            _chatViewModel.RequestSendFile();
        }
        private void AddItemToChatTemplate(Control control)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<UserControl>(AddItemToChatTemplate), control);
                return;
            }
            fpnChat.Controls.Add(control);
        }
    }
}
