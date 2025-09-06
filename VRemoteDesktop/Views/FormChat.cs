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
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private static readonly Color SelectedColor = Color.LightSkyBlue;
        private static readonly Color DefaultColor = Color.White;
        private ChatViewModel _chatViewModel;
        public FormChat()
        {
            InitializeComponent();
            SetupComponent();
        }
        #region Form Events
        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(value);
            if (value && this.WindowState == FormWindowState.Normal)
            {
                PositionForm();
            }
        }

        private void FormChat_Load(object sender, EventArgs e)
        {

        }
        private void FormChat_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_chatViewModel != null)
            {
                _chatViewModel.ProgressBarEvent -= ProgressBarEventHandler;
                _chatViewModel.AddedEvent -= AddeddEventHandler;
                _chatViewModel.RemovedEvent -= RemovedEventHandler;
                _chatViewModel.UpdateEvent -= UpdateEventHandler;
                _chatViewModel.UpdateChatHistoryEvent -= UpdateChatHistoryEventHandler;
                _chatViewModel.ChangeConnectionActivateEvent -= ChangeConnectionActivateEventHandler;
                _chatViewModel.Dispose();
            }
            this.txtChatContent.KeyDown -= KeydownEventHandler;
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            SendMessage(txtChatContent.Text);
        }
        private void btnSendAttachment_Click(object sender, EventArgs e)
        {
            _chatViewModel.RequestSendFile();
        }
        private void fpnChat_MouseWheel(object sender, MouseEventArgs e)
        {
            if (fpnChat.VerticalScroll.Visible)
            {
                Console.WriteLine("Scroll visible, value: " + fpnChat.VerticalScroll.Value);
                if (fpnChat.VerticalScroll.Value == 0)
                {
                    Console.WriteLine("At the top, continue load 5 previous message");
                }
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
        #endregion
        #region Methods
        private void SetupComponent()
        {
            _chatViewModel = new ChatViewModel();
            _chatViewModel.ProgressBarEvent += ProgressBarEventHandler;
            _chatViewModel.AddedEvent += AddeddEventHandler;
            _chatViewModel.RemovedEvent += RemovedEventHandler;
            _chatViewModel.UpdateEvent += UpdateEventHandler;
            _chatViewModel.UpdateChatHistoryEvent += UpdateChatHistoryEventHandler;
            _chatViewModel.ChangeConnectionActivateEvent += ChangeConnectionActivateEventHandler;
            this.txtChatContent.KeyDown += KeydownEventHandler;
        }
        public void AddConnection(string id, VClient client)
        {
            _chatViewModel.AddConnection(id, client);
        }    
        private void PositionForm()
        {
            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(
                workingArea.Right - this.Width,
                workingArea.Bottom - this.Height - 120
            );
        }
        private void InsertFirst(Control parent, List<Control> children)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<Control, List<Control>>(InsertFirst), parent, children);
                return;
            }
            foreach (Control child in children)
            {
                parent.Controls.Add(child);
                parent.Controls.SetChildIndex(child, 0);
            }
            RefeshUI(fpnChat);
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
        #endregion
        #region Custom Events

        private void ChangeConnectionActivateEventHandler(object sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object, EventArgs>(ChangeConnectionActivateEventHandler), sender, e);
                return;
            }
            if (sender is Label lb && lb.Parent is FlowLayoutPanel flow)
            {
                bool isSameConnection = (string.Compare(lb.Name, _chatViewModel.GetCurrentConnectionActivate(), StringComparison.OrdinalIgnoreCase) == 0);
                if (isSameConnection)
                {
                    if(fpnChat.Controls.Count == 0)
                        _chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                    return;
                }
                if(_chatViewModel.IsValidConnection(lb.Name))
                {
                    foreach (var connection in flow.Controls.OfType<Label>())
                    {
                        connection.BackColor = DefaultColor;
                    }
                    _chatViewModel.SetCurrentConnectionActivate(lb.Name);
                    lb.BackColor = SelectedColor;
                    _chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                }
                else
                {
                    ProcessConnectionRemoved(lb.Name);
                    Log.ForContext("FileName", this.GetType().Name + nameof(ChangeConnectionActivateEventHandler)).Error("Does not exists connection with id: " + lb.Name + " in connections");
                }
            }
        }
        private void UpdateChatHistoryEventHandler(object sender, ChatUpdateChatHistoryEventArgs e)
        {
            ProcessUpdateChatHistory(e.Type, e.Controls);
        }
        private void ProcessUpdateChatHistory(ChatUpdateChatHistoryEventType type, List<Control> controls)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<ChatUpdateChatHistoryEventType, List<Control>>(ProcessUpdateChatHistory), type, controls);
                return;
            }
            if (type == ChatUpdateChatHistoryEventType.LoadHistory)
            {
                for (int i = fpnChat.Controls.Count - 1; i >= 0; i--)
                {
                    ProcessMessageRemoved(fpnChat.Controls[i]);
                }
                InsertFirst(fpnChat, controls);
            }
        }
        private void ProgressBarEventHandler(object sender, ChatControlProgressBarUpdateUIEventArgs e)
        {
            UpdateBar(e.FileLayout, e.Num);
        }
        private void UpdateBar(FileAttachmentLayout f, int num)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<FileAttachmentLayout, int>(UpdateBar), f, num);
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
                this.Invoke(new Action<object, ChatControlRemoveEventArgs>(RemovedEventHandler), sender, e);
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
            if (control is Label lb)
            {
                _chatViewModel.ChangeConnectionActivate(lb, EventArgs.Empty);
            }
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
        private void ProcessMessageRemoved(Control control)
        {
            if (control.Parent == fpnChat)
            {
                if (control is FileAttachmentLayout file)
                {
                    fpnChat.Controls.Remove(file);
                    file.AcceptSaveFile -= _chatViewModel.FileReceivedClickEventHandler;
                    file.Dispose();
                }
                else if (control is Label lb)
                {
                    fpnChat.Controls.Remove(lb);
                    lb.Dispose();
                }
                RefeshUI(fpnChat);
            }
        }
        private void ProcessMessageRemoved(string key)
        {
            var controls = fpnChat.Controls.Find(key, true);
            foreach (var ctl in controls)
            {
                if (ctl is FileAttachmentLayout file)
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
        #endregion
    }
}
