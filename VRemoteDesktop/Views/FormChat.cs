using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.ViewModels;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private ChatViewModel _chatViewModel;
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

            fpnChat.FlowDirection = FlowDirection.TopDown;
            fpnChat.WrapContents = false;
            fpnChat.AutoScroll = true;
            fpnChat.BorderStyle = BorderStyle.FixedSingle;
            fpnChat.Padding = new Padding(0, 0, SystemInformation.VerticalScrollBarWidth, 0);

            fpnNumberChatConnection.BorderStyle = BorderStyle.FixedSingle;
            fpnNumberChatConnection.FlowDirection = FlowDirection.TopDown;
            fpnNumberChatConnection.WrapContents = false;
            fpnNumberChatConnection.BackColor = Color.White;
        }
        public ChatViewModel ChatView
        {
            get => _chatViewModel;
            private set => _chatViewModel = value;
        }
        private void SetupBinding()
        {
            _chatViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        private void Add()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(Add));
                return;
            }
            var value = _chatViewModel.ClientAdded;

            var control = _chatViewModel.NewControl(value);

            fpnNumberChatConnection.Controls.Add(control);
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
                Add();
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
    }
}
