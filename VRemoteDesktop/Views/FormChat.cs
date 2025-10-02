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
using static VRemoteDesktop.Utils.Logger;
using static VRemoteDesktop.Utils.DefaultForm;
using System.Security.Cryptography;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private static readonly Color SelectedColor = Color.LightSkyBlue;
        private static readonly Color DefaultColor = Color.White;
        private ChatViewModel _chatViewModel;
        private Dictionary<string, UserChatControl> _userChatControls;
        private ConcurrentDictionary<string, FileAttachmentLayout> _attachments;

        public FormChat()
        {
            InitializeComponent();
            SetupComponent();
            _userChatControls = new Dictionary<string, UserChatControl>();
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
                _chatViewModel.ProgressBarUpdateEvent -= ProgressBarUpdateEventHandler;
                _chatViewModel.AddedEvent -= AddedEventHandler;
                _chatViewModel.RemovedEvent -= RemovedEventHandler;
                _chatViewModel.UpdateEvent -= UpdateEventHandler;
                _chatViewModel.UpdateChatHistoryEvent -= UpdateChatHistoryEventHandler;
                _chatViewModel.ErrorEvent -= ShowErrorEvent;
                _chatViewModel.Dispose();
            }
            this.txtChatContent.KeyDown -= KeyDownEventHandler;
            _attachments.Clear();
        }
        private void btnSend_Click(object sender, EventArgs e)
        {
            SendMessage(txtChatContent.Text);
        }
        private void btnSendAttachment_Click(object sender, EventArgs e)
        {
            RespondHandler(_chatViewModel.RequestSendFile());
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
        private void KeyDownEventHandler(object sender, KeyEventArgs e)
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
            _chatViewModel.ProgressBarUpdateEvent += ProgressBarUpdateEventHandler;
            _chatViewModel.AddedEvent += AddedEventHandler;
            _chatViewModel.RemovedEvent += RemovedEventHandler;
            _chatViewModel.UpdateEvent += UpdateEventHandler;
            _chatViewModel.UpdateChatHistoryEvent += UpdateChatHistoryEventHandler;
            _chatViewModel.ErrorEvent += ShowErrorEvent;

            this.txtChatContent.KeyDown += KeyDownEventHandler;

            _attachments = new ConcurrentDictionary<string, FileAttachmentLayout>();
        }
        public void AddConnection(string id, VClient client)
        {
            RespondHandler(_chatViewModel.AddConnection(id, client));
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
                RespondHandler(_chatViewModel.SendChatMessage(content));
                txtChatContent.Clear();
                txtChatContent.Select();
            }
        }
        private void RespondHandler<T>(ChatRespond<T> respond)
        {
            switch (respond.Status)
            {
                case ChatRespondStatus.Success:
                    ProcessSuccessHandler(respond);
                    break;
                case ChatRespondStatus.Failed:
                    ProcessFailedHandler(respond);
                    break;
                case ChatRespondStatus.Error:
                    ProcessErrorHandler(respond);
                    break;
                case ChatRespondStatus.Timeout:
                    ProcessTimeoutHandler(respond);
                    break;
                default:
                    Log.ForContext("FileName", this.GetType().Name + "-" + nameof(RespondHandler)).Warning("Unexpected respond type");
                    break;
            }
        }
        private void ProcessSuccessHandler<T>(ChatRespond<T> respond)
        {
            Log.ForContext("FileName", this.GetType().Name + "-" + nameof(ProcessSuccessHandler)).Info(respond.SystemMessage);
            if (!string.IsNullOrWhiteSpace(respond.Message))
                MessageBox.Show(respond.Message, FORM_SUCCESS_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ProcessFailedHandler<T>(ChatRespond<T> respond)
        {
            Log.ForContext("FileName", this.GetType().Name + "-" + nameof(ProcessFailedHandler)).Error(respond.SystemMessage);
            if (!string.IsNullOrWhiteSpace(respond.Message))
                MessageBox.Show(respond.Message, FORM_FAILED_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        private void ProcessErrorHandler<T>(ChatRespond<T> respond)
        {
            Log.ForContext("FileName", this.GetType().Name + "-" + nameof(ProcessErrorHandler)).Error(respond.SystemMessage);
            if (!string.IsNullOrWhiteSpace(respond.Message))
                MessageBox.Show(respond.Message, FORM_ERROR_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        private void ProcessTimeoutHandler<T>(ChatRespond<T> respond)
        {
            Log.ForContext("FileName", this.GetType().Name + "-" + nameof(ProcessTimeoutHandler)).Error(respond.SystemMessage);
            if (!string.IsNullOrWhiteSpace(respond.Message))
                MessageBox.Show(respond.Message, FORM_TIMEOUT_TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    //MessageBox.Show($"{e.Ex.GetType().Name}: {e.Ex.Message}",
                    //               "Lỗi nghiêm trọng",
                    //               MessageBoxButtons.OK,
                    //               MessageBoxIcon.Error);
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
                if (sender is UserConnectionControl lb && lb.Parent is FlowLayoutPanel flow)
                {
                    var respond = _chatViewModel.GetCurrentConnectionActivate();
                    if (respond.IsSuccess)
                    {
                        bool isSameConnection = (string.Compare(lb.Name, respond.Data, StringComparison.OrdinalIgnoreCase) == 0);
                        if (isSameConnection)
                        {
                            //Load chat history on current connection
                            //if (fpnChat.Controls.Count == 0)
                            //    _chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                            return;
                        }
                    }
                    var isValidConnection = _chatViewModel.IsValidConnection(lb.Name);
                    RespondHandler(isValidConnection);
                    if (isValidConnection.IsSuccess)
                    {
                        foreach (var connection in flow.Controls.OfType<UserConnectionControl>())
                        {
                            connection.BackColor = DefaultColor;
                        }
                        var sp = _chatViewModel.SetCurrentConnectionActivate(lb.Name);
                        RespondHandler(sp);
                        if (sp.IsSuccess)
                        {
                            lb.BackColor = SelectedColor;
                            lb.ClearCount();
                            //Load chat history on current connection
                            //_chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                        }
                        ChangeChatContentByConnectionId(lb.Name);
                    }
                    else
                    {
                        ProcessConnectionRemoved(lb.Name);
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
                    var respond = _chatViewModel.GetConnectionNameById(e.ConnectionId);
                    RespondHandler(respond);
                    if (respond.IsSuccess)
                        CreateMessageControl(respond.Data, chatMessage);
                }
            }
            RefreshUI(fpnChat);
        }
        private void CreateMessageControl(string name, ChatText chatMessage)
        {
            Label lb = new Label
            {
                Text = (chatMessage.Owner == ChatOwnerEnum.Me ? "Me" : name) + ": "+ chatMessage.Message ,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
            };
            var currentConnectionId = _chatViewModel.GetCurrentConnectionActivate().Data;
            if (string.IsNullOrEmpty(currentConnectionId))
                return;
            InvokeAction(() => AddMessageToChatByConnectionId(currentConnectionId, lb));
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
                    RefreshUI(fpnChat);
                }
            });
        }
        private void ProgressBarUpdateEventHandler(object sender, ChatControlProgressBarUpdateUIEventArgs e)
        {
            if (_attachments.TryGetValue(e.FileId, out var attachment))
            {
                if(e.Status == FileStatus.CheckSumFailed)
                {
                    //This only check 
                    attachment.UpdateRequestSendFileStatus("File lỗi");
                    _attachments.TryRemove(e.FileId, out _);
                }
                else
                {
                    if (e.Status == FileStatus.Finished)
                        _attachments.TryRemove(e.FileId, out _);

                    InvokeAction(()
                        => UpdateBar(attachment, e.Num));
                }
            }
        }
        private void UpdateBar(FileAttachmentLayout f, int num)
        {
            f.UpdateProgressBar(num);
                     
        }
        private void AddedEventHandler(object sender, ChatControlAddedEventArgs e)
        {
            InvokeAction(() =>
            {
                if (e.Type == ChatControlType.Connection)
                {
                    ProcessConnectionAdded(e.ConnectionId, e.Content);
                    RefreshUI(fpnNumberChatConnection);
                }
                else if (e.Type == ChatControlType.Message)
                {
                    ProcessMessageAdded(e.ConnectionId, e.Content, e.ConnectionName);
                    RefreshUI(fpnChat);
                }
                else if (e.Type == ChatControlType.RequestAttachment || e.Type == ChatControlType.ReceivedAttachment)
                {
                    ProcessAttachmentAdded(e.Type, e.ConnectionId, e.FileInfo);
                    RefreshUI(fpnChat);
                }
                else
                {
                    MessageBox.Show("Unexpected event type " + this.GetType().Name + " - " + nameof(AddedEventHandler), "error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        }
        private void ProcessAttachmentAdded(ChatControlType type, string connectionId, VFileInfo fileInfo)
        {
            var userChatControl = GetUserChatControl(connectionId);
            if (userChatControl == null)
                return;

            userChatControl.AddAttachment(type, connectionId, fileInfo);
            var chats = FindUserConnectionControlsById(connectionId);
            string curId = _chatViewModel.GetCurrentConnectionActivate().Data;
            foreach (var item in chats)
            {
                if(item.Name != curId)
                    item.UpdateUnreadCount(1);
            }
        }
        private void FileEventHandler(object sender, P2PFileReceivedEventArgs e)
        {
            InvokeAction(() =>
            {
                if (sender is Button btn && btn.Parent is FileAttachmentLayout parent)
                {
                    //Accept file
                    if (string.Compare(btn.Name, "btnSave") == 0)
                    {
                        var updatePathRespond = _chatViewModel.UpdateFileSavePath(parent.Id, e.FilePath);
                        RespondHandler(updatePathRespond);
                        if (updatePathRespond.IsSuccess)
                        {
                            var saveFileRespond =  _chatViewModel.SaveChatToFile(parent.SocketId, parent.FileInfo.SavePath, parent.FileInfo.Filename, parent.FileInfo.FileSize);
                            RespondHandler(saveFileRespond);
                            if(saveFileRespond.IsSuccess)
                            {
                                var respond = _chatViewModel.AcceptedFile(parent.Id);
                                RespondHandler(respond);
                                if (respond.IsSuccess)
                                    parent.AcceptSendFile();
                            }
                        }
                    }
                    //Reject file
                    else if (string.Compare(btn.Name, "btnCancel") == 0)
                    {
                        var respond = _chatViewModel.DeclinedFile(parent.Id);
                        RespondHandler(respond);
                        if (respond.IsSuccess)
                            parent.RejectSendFile();

                        _attachments.TryRemove(parent.Id, out _);
                    }
                    else if (string.Compare(btn.Name, "btnStop") == 0)
                    {
                        var respond = _chatViewModel.StopReceivedFileDataByFileId(parent.Id);
                        RespondHandler(respond);
                        if (respond.IsSuccess)
                        {
                            parent.DisableControl(btn);
                            parent.RemoveProgressBar();
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

                    else if (e.Type == ChatControlType.RefuseAttachment)
                        attachment.UpdateRequestSendFileStatus("Đối tác đã từ chối");

                    else if(e.Type == ChatControlType.StopSendingAttachment)
                        attachment.UpdateRequestSendFileStatus("Đối tác hủy nhận file");
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
            UserConnectionControl lb = new UserConnectionControl(connectionId, text, fpnNumberChatConnection.Width - 2 , 25);
            lb.Click += ChangeConnectionActivateEventHandler;

            //Label lbChat = new Label
            //{
            //    Text = text,
            //    Name = connectionId,
            //    BackColor = Color.LightSkyBlue,
            //    BorderStyle = BorderStyle.FixedSingle,
            //    AutoSize = false,
            //    Height = 20,
            //    Margin = Padding.Empty
            //};
            //lbChat.Click += ChangeConnectionActivateEventHandler;
            //lbChat.Width = fpnNumberChatConnection.Width - 2;
            fpnNumberChatConnection.Controls.Add(lb);
            ChangeConnectionActivateEventHandler(lb, EventArgs.Empty);
            RefreshUI(fpnNumberChatConnection);
            fpnNumberChatConnection.ScrollControlIntoView(lb);
        }
        private void ProcessMessageAdded(string connectionId, string message, string connectionName)
        {
            Label lb = new Label
            {
                Text = (connectionName ?? "Tôi") + ": " + message,
                AutoSize = true,
                TextAlign = ContentAlignment.TopLeft,
            };

            lb.MaximumSize = new Size(fpnChat.Width - SystemInformation.VerticalScrollBarWidth - 10, 0);

            AddMessageToChatByConnectionId(connectionId, lb);
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
            RefreshUI(fpnNumberChatConnection);
        }
        private void ProcessMessageRemoved(Control control)
        {
            if (control.Parent == fpnChat)
            {
                if (control is FileAttachmentLayout file)
                {
                    fpnChat.Controls.Remove(file);
                    file.AcceptSaveFile -= FileEventHandler;
                    file.Dispose();
                }
                else if (control is Label lb)
                {
                    fpnChat.Controls.Remove(lb);
                    lb.Dispose();
                }
                RefreshUI(fpnChat);
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
                    file.AcceptSaveFile -= FileEventHandler;
                    file.Dispose();
                }
                else if (ctl is Label lb)
                {
                    fpnChat.Controls.Remove(lb);
                    lb.Dispose();
                }
            }
            RefreshUI(fpnChat);
        }
        private void InvokeAction(Action action)
        {
            if (this.InvokeRequired)
                this.Invoke((MethodInvoker)(() => action()));
            else
                action();
        }
        private List<UserConnectionControl> FindUserConnectionControlsById(string id)
        {
            var matches = fpnNumberChatConnection.Controls.Find(id, true);
            return matches.OfType<UserConnectionControl>().ToList();
        }
        private void RefreshUI(Control control)
        {
            control.PerformLayout();
            control.Refresh();
            control.Invalidate();
        }
        private void AddMessageToChatByConnectionId(string connectionId, Control control)
        {
            var userChatControl = GetUserChatControl(connectionId);
            if (userChatControl == null)
                return;

            userChatControl.AddControl(control);
            var chats = FindUserConnectionControlsById(connectionId);
            string curId = _chatViewModel.GetCurrentConnectionActivate().Data;
            foreach (var item in chats)
            {
                if (item.Name != curId)
                    item.UpdateUnreadCount(1);
            }
        }
        private UserChatControl GetUserChatControl(string connectionId)
        {
            void ResizeToParent(Control parent, Control child)
            {
                child.Margin = new Padding(0);
                child.Width = parent.ClientSize.Width;
                child.Height = parent.ClientSize.Height - parent.Padding.Vertical;
            }

            if(_userChatControls.TryGetValue(connectionId, out var userChatControl))
            {
                if (!fpnChat.Controls.Contains(userChatControl))
                {
                    ResizeToParent(fpnChat, userChatControl);
                }
                return userChatControl;
            }
            else
            {
                _userChatControls[connectionId] = new UserChatControl(connectionId);
                _userChatControls[connectionId].UserChatEvent += UserChatEventHandler;
                if (!fpnChat.Controls.Contains(_userChatControls[connectionId]))
                {
                    ResizeToParent(fpnChat, _userChatControls[connectionId]);
                }
                return _userChatControls[connectionId];
            }
        }
        private void UserChatEventHandler(object sender, UserChatControlEventArgs e)
        {
            Console.WriteLine(string.Format("{0} - {1}", e.Type, e.Id));
        }
        private void ChangeChatContentByConnectionId(string connectionId)
        {
           if(_userChatControls.TryGetValue(connectionId, out var userChatControls))
           {
                foreach(Control item in fpnChat.Controls)
                {
                    ProcessMessageRemoved(item);
                }
                fpnChat.Controls.Clear();
                fpnChat.Controls.Add(userChatControls);
                RefreshUI(fpnChat);
           }
        }
        #endregion
    }
}
