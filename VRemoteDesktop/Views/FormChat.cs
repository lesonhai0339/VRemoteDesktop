using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.ViewModels;
using static System.Net.Mime.MediaTypeNames;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private ChatViewModel _chatViewModel;
        public FormChat()
        {
            InitializeComponent();
            Setup();
        }
        public void AddConnection(string id, VClient client)
        {
            _chatViewModel.AddConnection(id, client);
        }
        private void Setup()
        {
            _chatViewModel = new ChatViewModel();
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

            _chatViewModel.ProgressBarEvent += ProgressBarEventHandler;
            _chatViewModel.AddeddEvent += AddeddEventHandler;
            _chatViewModel.RemovedEvent += RemovedEventHandler;
            _chatViewModel.UpdateEvent += UpdateEventHandler;

            this.txtChatContent.KeyDown += KeydownEventHandler;
        }
        private void FormChat_Load(object sender, EventArgs e)
        {

        }
        private void FormChat_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(_chatViewModel != null)
            {
                _chatViewModel.ProgressBarEvent -= ProgressBarEventHandler;
                _chatViewModel.AddeddEvent -= AddeddEventHandler;
                _chatViewModel.RemovedEvent -= RemovedEventHandler;
                _chatViewModel.UpdateEvent -= UpdateEventHandler;
                _chatViewModel.Dispose();
            }
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            SendMessage(txtChatContent.Text);
        }
        private void btnSendAttachment_Click(object sender, EventArgs e)
        {
            _chatViewModel.RequestSendFile();
        }
        private void SendMessage(string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                _chatViewModel.SendChatMessage(content);
                txtChatContent.Clear();
                txtChatContent.Select();
            }
        }
        private void KeydownEventHandler(object sender, KeyEventArgs e)
        {
            if (Form.ActiveForm == this)
            {
                if (e.KeyCode == Keys.Return)
                {
                    SendMessage(txtChatContent.Text);
                }
            }
        }
        private void ProgressBarEventHandler(object sender, ChatControlProgressBarUpdateEventArgs e)
        {

            UpdateBar(e.FileLayout, e.Num);
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
        private void AddeddEventHandler(object sender, ChatControlAddedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, ChatControlAddedEventArgs>(AddeddEventHandler), sender, e);
                return;
            }
            if (e.Type == ChatControlType.Connection)
            {
                ProcessConnectionAdded(e.Control);
            }
            else if (e.Type == ChatControlType.Message)
            {
                ProcessMessageAdded(e.Control);
            }
            else
            {
                MessageBox.Show("Unexpected event type " + this.GetType().Name + " - " + nameof(AddeddEventHandler), "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void UpdateEventHandler(object sender, ChatControlUpdateEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, ChatControlUpdateEventArgs>(UpdateEventHandler), sender, e);
                return;
            }
            ProcessUpdateEvent(e.Action);
        }
        private void RemovedEventHandler(object sender, ChatControlRemoveEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, ChatControlRemoveEventArgs>(RemovedEventHandler), sender,e);
                return;
            }
            if (e.Type == ChatControlType.Connection)
            {
                ProcessConnectionRemoved(e.ControlKey);
            }
            else if (e.Type == ChatControlType.Message)
            {
                ProcessMessageRemoved(e.ControlKey);
            }
            else
            {
                MessageBox.Show("Unexpected event type " + this.GetType().Name + " - " + nameof(RemovedEventHandler), "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ProcessUpdateEvent(Action action)
        {
            action();
        }
        private void ProcessConnectionAdded(Control control)
        {
            control.Width = fpnNumberChatConnection.Width - 2;
            fpnNumberChatConnection.Controls.Add(control);
            RefeshUI(fpnNumberChatConnection);
            fpnNumberChatConnection.ScrollControlIntoView(control);
        }
        private void ProcessMessageAdded(Control control)
        {
            control.MaximumSize = new Size(fpnChat.Width - SystemInformation.VerticalScrollBarWidth - 10, 0);
            fpnChat.Controls.Add(control);
            RefeshUI(fpnChat);
            fpnChat.ScrollControlIntoView(control);
        }
        private void ProcessConnectionRemoved(string key)
        {      
            var controls = fpnNumberChatConnection.Controls.Find(key, true);
            foreach (var ctl in controls)
            {
                fpnNumberChatConnection.Controls.Remove(ctl);
                ctl.Click -= _chatViewModel.ChangeConnectionActivate;
                ctl.Dispose();
            }
            RefeshUI(fpnNumberChatConnection);
        }
        private void ProcessMessageRemoved(string key)
        {
            var controls = fpnChat.Controls.Find(key, true);
            foreach (var ctl in controls)
            {
                if (ctl is FileReceivedLayout file)
                {
                    fpnChat.Controls.Remove(file);
                    file.AcceptSaveFile -= _chatViewModel.FileReceivedClickEventHandler;
                    file.Dispose();
                }
                else if (ctl is Label lb)
                {
                    fpnChat.Controls.Remove(lb);
                    lb.Dispose();
                }
            }
            RefeshUI(fpnChat);
        }
        private void RefeshUI(Control control)
        {
            control.PerformLayout();
            control.Refresh();
            control.Invalidate();
        }
    }
}
