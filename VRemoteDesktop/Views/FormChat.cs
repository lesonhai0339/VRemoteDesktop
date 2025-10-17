using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using static VRemoteDesktop.Utils.DefaultForm;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private static readonly Color SelectedColor = Color.LightSkyBlue;
        private static readonly Color DefaultColor = Color.White;
        private ChatViewModel _chatViewModel;
        private Dictionary<string, ConnectionChatDataPanel> _userChatControls;

        public FormChat()
        {
            InitializeComponent();
            SetupComponent();

            _userChatControls = new Dictionary<string, ConnectionChatDataPanel>();
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
                _chatViewModel.ProgressBarUpdateEvent -= BarUpdateEventHandler;
                _chatViewModel.AddedEvent -= AddedEventHandler;
                _chatViewModel.RemovedEvent -= RemovedEventHandler;
                _chatViewModel.UpdateEvent -= UpdateEventHandler;
                _chatViewModel.UpdateChatHistoryEvent -= UpdateChatHistoryEventHandler;
                _chatViewModel.ErrorEvent -= ShowErrorEvent;
                _chatViewModel.Dispose();
            }

            foreach(var item in _userChatControls)
            {
                item.Value?.Dispose();
            }

            _userChatControls.Clear();

            this.txtChatContent.KeyDown -= KeyDownEventHandler;
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
            _chatViewModel.ProgressBarUpdateEvent += BarUpdateEventHandler;
            _chatViewModel.AddedEvent += AddedEventHandler;
            _chatViewModel.RemovedEvent += RemovedEventHandler;
            _chatViewModel.UpdateEvent += UpdateEventHandler;
            _chatViewModel.UpdateChatHistoryEvent += UpdateChatHistoryEventHandler;
            _chatViewModel.ErrorEvent += ShowErrorEvent;

            this.txtChatContent.KeyDown += KeyDownEventHandler;
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
            InvokeAction(() =>
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
            });
        }
        private void ChangeConnectionActivateEventHandler(object sender, EventArgs e)
        {
            if (sender is ConnectionChatPanel pn && pn.Parent is FlowLayoutPanel flow)
            {
                var respond = _chatViewModel.GetCurrentConnectionActivate();

                if (respond.IsSuccess)
                {
                    bool isSameConnection = (string.Compare(pn.Name, respond.Data, StringComparison.OrdinalIgnoreCase) == 0);

                    if (isSameConnection)
                    {
                        //Load chat history on current connection
                        //if (fpnChat.Controls.Count == 0)
                        //    _chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                        return;
                    }
                }

                var isValidConnection = _chatViewModel.IsValidConnection(pn.Name);
                RespondHandler(isValidConnection);

                if (isValidConnection.IsSuccess)
                {
                    foreach (var connection in flow.Controls.OfType<ConnectionChatPanel>())
                    {
                        connection.BackColor = DefaultColor;
                    }

                    var sp = _chatViewModel.SetCurrentConnectionActivate(pn.Name);
                    RespondHandler(sp);

                    if (sp.IsSuccess)
                    {
                        pn.BackColor = SelectedColor;
                        pn.ClearCount();
                        //Load chat history on current connection
                        //_chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                    }

                    ChangeChatDataByConnectionId(pn.Name);
                }
                else
                {
                    ProcessConnectionRemoved(pn.Id, pn.Name);
                }
            }
        }
        private void UpdateChatHistoryEventHandler(object sender, ChatUpdateChatHistoryEventArgs e)
        {
            InvokeAction(() =>
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
            });
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

            AddChatDataByConnectionId(currentConnectionId, lb);
        }
        private void BarUpdateEventHandler(object sender, ChatControlProgressBarUpdateUIEventArgs e)
        {
            var userChat = GetConnectionChatDataPanelByConnectionId(e.ConnectionId);

            if(userChat != null)
            {
                InvokeAction(()=> userChat.UpdateProgressBar(e.FileId, e.Status, e.Num));
            }
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
            var userChatControl = GetConnectionChatDataPanelByConnectionId(connectionId);

            if (userChatControl == null)
                return;

            userChatControl.AddAttachment(type, connectionId, fileInfo);
            var chats = FindUserConnectionControlsById(connectionId);
            string curId = _chatViewModel.GetCurrentConnectionActivate().Data;

            foreach (var item in chats)
            {
                if (item.Name != curId)
                    item.UpdateUnreadCount(1);
            }
        }
        private void UpdateEventHandler(object sender, ChatControlUpdateEventArgs e)
        {
            InvokeAction(() =>
            {
                var userChat = GetConnectionChatDataPanelByConnectionId(e.ConnectionId);

                if(userChat != null)
                {
                    userChat.UpdateAttachmentStatus(e.FileId, e.Type);  
                }
                
            });
        }
        private void RemovedEventHandler(object sender, ChatControlRemoveEventArgs e)
        {
            InvokeAction(() =>
            {
                if (e.Type == ChatControlType.Connection)
                {
                    ProcessConnectionRemoved(e.ConnectionId, e.ControlKey);
                    return;
                }

                if (e.Type == ChatControlType.Message)
                {
                    ProcessMessageRemoved(e.ConnectionId, e.ControlKey);
                    return;
                }
            });
        }
        private void ProcessConnectionAdded(string connectionId, string text)
        {
            ConnectionChatPanel lb = new ConnectionChatPanel(connectionId, text, fpnNumberChatConnection.Width - 2, 25);
            lb.Click += ChangeConnectionActivateEventHandler;
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

            AddChatDataByConnectionId(connectionId, lb);
        }
        private void ProcessConnectionRemoved(string connectionId, string key)
        {
            var controls = fpnNumberChatConnection.Controls.Find(key, true);

            foreach (var ctl in controls)
            {
                fpnNumberChatConnection.Controls.Remove(ctl);
                ctl.Click -= ChangeConnectionActivateEventHandler;
                ctl.Dispose();
            }

            RemoveChatByConnectionId(connectionId);
            RefreshUI(fpnNumberChatConnection);
        }
        private void ProcessMessageRemoved(string connectionId, Control control)
        {
            var chatDataPanel = GetConnectionChatDataPanelByConnectionId(connectionId);

            if (chatDataPanel != null)
            {
                chatDataPanel.RemoveControlByKey(control);
            }

            RefreshUI(fpnChat);
        }
        private void ProcessMessageRemoved(string connectionId, string key)
        {
            var chatDataPanel = GetConnectionChatDataPanelByConnectionId(connectionId);

            if(chatDataPanel != null)
            {
                chatDataPanel.RemoveControlByKey(key);   
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
        private List<ConnectionChatPanel> FindUserConnectionControlsById(string id)
        {
            var matches = fpnNumberChatConnection.Controls.Find(id, true);
            return matches.OfType<ConnectionChatPanel>().ToList();
        }
        private void RefreshUI(Control control)
        {
            control.PerformLayout();
            control.Refresh();
            control.Invalidate();
        }
        private void AddChatDataByConnectionId(string connectionId, Control control)
        {
            var userChatControl = GetConnectionChatDataPanelByConnectionId(connectionId);

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
        void ResizeToParent(Control parent, Control child)
        {
            child.Margin = new Padding(0);
            child.Width = parent.ClientSize.Width;
            child.Height = parent.ClientSize.Height;
        }
        private ConnectionChatDataPanel GetConnectionChatDataPanelByConnectionId(string connectionId)
        {
            if(_userChatControls.TryGetValue(connectionId, out var chatDataPanel))
            {
                if (!fpnChat.Controls.Contains(chatDataPanel))
                {
                    ResizeToParent(fpnChat, chatDataPanel);
                }

                return chatDataPanel;
            }
            else
            {
                _userChatControls[connectionId] = new ConnectionChatDataPanel(connectionId);
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
            if (e.Type == UserChatControlEventType.AttachmentAccepted)
            {
                var updatePathRespond = _chatViewModel.UpdateFileSavePath(e.Attachment.Id, e.Path);
                RespondHandler(updatePathRespond);

                if (updatePathRespond.IsSuccess)
                {
                    var saveFileRespond = _chatViewModel.SaveChatToFile(e.Attachment.SocketId, e.Attachment.FileInfo.SavePath, e.Attachment.FileInfo.Filename, e.Attachment.FileInfo.FileSize);
                    RespondHandler(saveFileRespond);

                    if (saveFileRespond.IsSuccess)
                    {
                        _chatViewModel.AcceptedFile(e.Attachment.Id);
                    }
                }
            }
            //Refused file
            else if (e.Type == UserChatControlEventType.AttachmentRefused)
            {
                _chatViewModel.DeclinedFile(e.Attachment.Id);
            }
            //Stopped receive file
            else if (e.Type == UserChatControlEventType.AttachmentStopped)
            {
               _chatViewModel.StopReceivedFileDataByFileId(e.Attachment.Id);
            }
        }
        private void RemoveChatByConnectionId(string connectionId)
        {
            var currentConnectionId = _chatViewModel.GetCurrentConnectionActivate().Data;

            if (!string.IsNullOrEmpty(currentConnectionId))
                return;

            _userChatControls.Remove(connectionId);

            if (connectionId.Equals(currentConnectionId))
            {
                foreach (Control item in fpnChat.Controls)
                {
                    ProcessMessageRemoved(connectionId ,item);
                }

                fpnChat.Controls.Clear();
            }
        }
        private void ChangeChatDataByConnectionId(string connectionId)
        {
            var connectionChatMessages = GetConnectionChatDataPanelByConnectionId(connectionId);

            if(connectionChatMessages != null)
            {
                foreach (Control item in fpnChat.Controls)
                {
                    ProcessMessageRemoved(connectionId, item);
                }

                fpnChat.Controls.Clear();
                fpnChat.Controls.Add(connectionChatMessages);

                RefreshUI(fpnChat);
            }
        }
        #endregion
    }
}
