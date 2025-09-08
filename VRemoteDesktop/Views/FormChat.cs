using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.ViewModels;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private static readonly Color SelectedColor = Color.LightSkyBlue;
        private static readonly Color DefaultColor = Color.White;
        private ChatViewModel _chatViewModel;
        private ConcurrentDictionary<string, FileAttachmentLayout> _attachments;

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
                _chatViewModel.FileClickedEvent -= FileReceivedClickEventHandler;
                _chatViewModel.ErrorEvent -= ShowErrorEvent;
                _chatViewModel.Dispose();
            }
            this.txtChatContent.KeyDown -= KeydownEventHandler;
            _attachments.Clear();
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
                //Console.WriteLine("Scroll visible, value: " + fpnChat.VerticalScroll.Value);
                if (fpnChat.VerticalScroll.Value == 0)
                {
                    //Console.WriteLine("At the top, continue load 5 previous message");
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
            _chatViewModel.FileClickedEvent += FileReceivedClickEventHandler;
            _chatViewModel.ErrorEvent += ShowErrorEvent;

            this.txtChatContent.KeyDown += KeydownEventHandler;

            _attachments = new ConcurrentDictionary<string, FileAttachmentLayout>();
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
            foreach (Control child in children)
            {
                parent.Controls.Add(child);
                parent.Controls.SetChildIndex(child, 0);
            }
        }
        private void InsertFirst(Control parent, Control child)
        {
            parent.Controls.Add(child);
            parent.Controls.SetChildIndex(child, 0);
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
        private void ShowErrorEvent(object sender, ChatErrorEventArgs e)
        {
            var logger = Log.ForContext("FileName", this.GetType().Name);

            switch (e.Level)
            {
                case ChatErrorLevel.Critical:
                    logger.Error(e.Ex, e.Ex.Message);
                    MessageBox.Show($"{e.Ex.GetType().Name}: {e.Ex.Message}",
                                   "Lỗi nghiêm trọng",
                                   MessageBoxButtons.OK,
                                   MessageBoxIcon.Error);
                    break;

                case ChatErrorLevel.Warning:
                    logger.Warning(e.Ex, e.Ex.Message);
                    break;

                case ChatErrorLevel.Info:
                    logger.Info(e.Ex, e.Ex.Message);
                    break;
            }
        }
        private void ChangeConnectionActivateEventHandler(object sender, EventArgs e)
        {
            InvokeAction(() =>
            {
                if (sender is Label lb && lb.Parent is FlowLayoutPanel flow)
                {
                    bool isSameConnection = (string.Compare(lb.Name, _chatViewModel.GetCurrentConnectionActivate(), StringComparison.OrdinalIgnoreCase) == 0);
                    if (isSameConnection)
                    {
                        if (fpnChat.Controls.Count == 0)
                            _chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                        return;
                    }
                    if (_chatViewModel.IsValidConnection(lb.Name))
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
            });
        }
        private void UpdateChatHistoryEventHandler(object sender, ChatUpdateChatHistoryEventArgs e)
        {
            if (e.Messages == null || e.Messages.Length == 0)
            {
                Log.ForContext("FileName", this.GetType().Name + " - " + nameof(UpdateChatHistoryEventHandler))
                    .Error("Message is null or empty");
                return;
            }
            foreach (var message in e.Messages)
            {
                if (message is ChatFile chatFile)
                {
                    //TODO: not implement, missing file Id to create FileAttachmentLayout, will handler soon
                }
                if (message is ChatText chatMessage)
                {
                    string name = _chatViewModel.GetConnectionNameById(e.ConnectionId);
                    CreateMessageControl(name, chatMessage);
                }
            }
            RefeshUI(fpnChat);
        }
        private void CreateMessageControl(string name, ChatText chatMessage)
        {
            Label lb = new Label
            {
                Text = (chatMessage.Owner == ChatOwnerEnum.Me ? "Me" : name) + ": "+ chatMessage.Message ,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
            };
            InvokeAction(()=> InsertFirst(fpnChat, lb));
        }
        private void ProcessUpdateChatHistory(ChatUpdateChatHistoryEventType type, List<Control> controls)
        {
            InvokeAction(() =>
            {
                if (type == ChatUpdateChatHistoryEventType.LoadHistory)
                {
                    for (int i = fpnChat.Controls.Count - 1; i >= 0; i--)
                    {
                        ProcessMessageRemoved(fpnChat.Controls[i]);
                    }
                    InsertFirst(fpnChat, controls);
                    RefeshUI(fpnChat);
                }
            });
        }
        private void ProgressBarEventHandler(object sender, ChatControlProgressBarUpdateUIEventArgs e)
        {
            if (_attachments.TryGetValue(e.FileId, out var attachment))
            {
                if (e.Status == FileStatus.Finished)
                    _attachments.TryRemove(e.FileId, out _);

                InvokeAction(() 
                    => UpdateBar(attachment, e.Num));  
            }
        }
        private void UpdateBar(FileAttachmentLayout f, int num)
        {
            f.UpdateProgressBar(num);
                     
        }
        private void AddeddEventHandler(object sender, ChatControlAddedEventArgs e)
        {
            InvokeAction(() =>
            {
                if (e.Type == ChatControlType.Connection)
                {
                    ProcessConnectionAdded(e.ConnectionId, e.Content);
                    RefeshUI(fpnNumberChatConnection);
                }
                else if (e.Type == ChatControlType.Message)
                {
                    ProcessMessageAdded(e.ConnectionId, e.Content);
                    RefeshUI(fpnChat);
                }
                else if (e.Type == ChatControlType.RequestAttachment)
                {
                    ProcessAttachmentAdded(e.Type, e.ConnectionId, e.FileInfo);
                    RefeshUI(fpnChat);
                }
                else
                {
                    MessageBox.Show("Unexpected event type " + this.GetType().Name + " - " + nameof(AddeddEventHandler), "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }
        private void ProcessAttachmentAdded(ChatControlType type, string connectionId, VFileInfo fileInfo)
        {
            FileAttachmentLayout fileAttachmentLayout = new FileAttachmentLayout(fileInfo.Id, connectionId);
            fileAttachmentLayout.Add(fileInfo, true);
            if(type == ChatControlType.RequestAttachment)
            {
                _attachments.TryAdd(fileInfo.Id, fileAttachmentLayout);
            }
            if (type == ChatControlType.ReceivedAttachment)
            {
                _attachments[fileInfo.Id] = fileAttachmentLayout;
                fileAttachmentLayout.AcceptSaveFile += FileReceivedClickEventHandler;
            }
            fpnChat.Controls.Add(fileAttachmentLayout);
            fpnChat.ScrollControlIntoView(fileAttachmentLayout);
        }

        private void FileReceivedClickEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            InvokeAction(() =>
            {
                if (sender is Button btn && btn.Parent is FileAttachmentLayout parent)
                {
                    //Accept file
                    if (string.Compare(btn.Name, "btnSave") == 0)
                    {
                        _chatViewModel.UpdateFileSavePath(parent.Id, e.FilePath);
                        _chatViewModel.SaveFileChat(parent.SocketId, parent.FileInfo.SavePath, parent.FileInfo.Filename, parent.FileInfo.FileSize);

                        if (_chatViewModel.ProcessAcceptSendFile(parent.Id))
                        {
                            parent.AcceptSendFile();
                        }
                    }
                    //Reject file
                    if (string.Compare(btn.Name, "btnCancel") == 0)
                    {
                        if (_chatViewModel.ProcessRejectSendFile(parent.Id))
                        {
                            parent.RejectSendFile();
                        }
                        _attachments.TryRemove(parent.Id, out _);
                    }
                }
            });
        }
        private void UpdateEventHandler(object sender, ChatControlUpdateEventArgs e)
        {
            InvokeAction(() =>
            {
                if (_attachments.TryGetValue(e.FileId, out var attachment))
                {
                    if (e.Type == ChatControlType.AcceptAttachment)
                        attachment.UpdateRequestSendFileStatus("Đối tác đã chấp nhận");

                    if (e.Type == ChatControlType.RejectAttachment)
                        attachment.UpdateRequestSendFileStatus("Đối tác đã từ chối");
                }
                else
                {
                    Log.ForContext("FileName", this.GetType().Name + " - " + nameof(UpdateEventHandler)).Error("Cannot find attachment with id: " + e.FileId);
                }
            });
        }
        private void RemovedEventHandler(object sender, ChatControlRemoveEventArgs e)
        {
            InvokeAction(() =>
            {
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
            });
        }
        private void ProcessConnectionAdded(string connectionId, string text)
        {
            Label lbChat = new Label
            {
                Text = text,
                Name = connectionId,
                BackColor = Color.LightSkyBlue,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = false,
                Height = 20,
                Margin = Padding.Empty
            };
            lbChat.Click += ChangeConnectionActivateEventHandler;
            lbChat.Width = fpnNumberChatConnection.Width - 2;
            fpnNumberChatConnection.Controls.Add(lbChat);
            if (lbChat is Label lb)
            {
                ChangeConnectionActivateEventHandler(lb, EventArgs.Empty);
            }
            RefeshUI(fpnNumberChatConnection);
            fpnNumberChatConnection.ScrollControlIntoView(lbChat);
        }
        private void ProcessMessageAdded(string connectionId, string message)
        {
            Label lb = new Label
            {
                Text = "Me: " + message,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
            };

            lb.MaximumSize = new Size(fpnChat.Width - SystemInformation.VerticalScrollBarWidth - 10, 0);
            fpnChat.Controls.Add(lb);
            RefeshUI(fpnChat);
            fpnChat.ScrollControlIntoView(lb);
        }
        private void ProcessConnectionRemoved(string key)
        {
            var controls = fpnNumberChatConnection.Controls.Find(key, true);
            foreach (var ctl in controls)
            {
                fpnNumberChatConnection.Controls.Remove(ctl);
                ctl.Click -= ChangeConnectionActivateEventHandler;
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
            if (controls.Length == 0)
                MessageBox.Show("Không tồn tại tin nhắn với id: " + key, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);

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
        private void InvokeAction(Action action)
        {
            if (this.InvokeRequired)
                this.Invoke((MethodInvoker)(() => action()));
            else
                action();
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
