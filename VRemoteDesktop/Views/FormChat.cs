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
using VRemoteDesktop.Presenters;
using VRemoteDesktop.Services.FileService;
using VRemoteDesktop.Services.FileService.DTOs;
using VRemoteDesktop.Services.FileService.Enums;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using VRemoteDesktop.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using static VRemoteDesktop.Utils.DefaultForm;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private const string LOG_FILE = "FormChat";
        private readonly Color SelectedColor = Color.LightSkyBlue;
        private readonly Color DefaultColor = Color.White;

        private readonly ChatPresenter _chatPresenter;
        private Dictionary<string, ConnectionChatDataPanel> _userChatControls;

        public FormChat(RemoteService remoteService)
        {
            InitializeComponent();

            _chatPresenter = new ChatPresenter(remoteService);
            _userChatControls = new Dictionary<string, ConnectionChatDataPanel>();

            _chatPresenter.ProgressBarUpdateEvent += BarUpdateEventHandler;
            _chatPresenter.AddedEvent += AddedEventHandler;
            _chatPresenter.RemovedEvent += RemovedEventHandler;
            _chatPresenter.UpdateEvent += UpdateEventHandler;
            _chatPresenter.UpdateChatHistoryEvent += UpdateChatHistoryEventHandler;
            _chatPresenter.ErrorEvent += ShowErrorEvent;

            this.txtChatContent.KeyDown += KeyDownEventHandler;
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
            if (_chatPresenter != null)
            {
                _chatPresenter.ProgressBarUpdateEvent -= BarUpdateEventHandler;
                _chatPresenter.AddedEvent -= AddedEventHandler;
                _chatPresenter.RemovedEvent -= RemovedEventHandler;
                _chatPresenter.UpdateEvent -= UpdateEventHandler;
                _chatPresenter.UpdateChatHistoryEvent -= UpdateChatHistoryEventHandler;
                _chatPresenter.ErrorEvent -= ShowErrorEvent;
                _chatPresenter.Dispose();
            }

            foreach (var item in _userChatControls)
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
            SendFile();
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
        public void AddChatSession(string sessionId, ClientSession clientSession)
        {
            try
            {
                _chatPresenter.AddToChat(sessionId, clientSession);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("", LOG_FILE).Error(ex, "AddChatSession err ");
            }
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
            try
            {
                txtChatContent.Clear();
                txtChatContent.Select();
                _chatPresenter.SendMessage(content);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("", LOG_FILE).Error(ex, "SendMessage err ");
                ShowMessageBox("Lỗi", "Không thể gủi tin nhắn", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void SendFile()
        {
            try
            {
                _chatPresenter.RequestSendFile();
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("", LOG_FILE).Error(ex, "SendFile err ");
                ShowMessageBox("Lỗi", "Gửi file thất bại", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ShowMessageBox(string title, string msg, MessageBoxButtons button, MessageBoxIcon icon)
        {
            MessageBox.Show(msg, title, button, icon);
        }
        #endregion
        #region Custom Events
        private void ShowErrorEvent(object sender, ChatErrorEventArgs e)
        {
            InvokeAction(() =>
            {
                var logger = Log.ForContext("FileName", LOG_FILE);

                switch (e.Level)
                {
                    case ChatErrorLevel.Critical:
                        logger.Error(e.Ex, e.Ex.Message);
                        MessageBox.Show($"{e.Message}",
                                       "Lỗi",
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
            });
        }
        private void ActiveSessionChatChanged(object sender, EventArgs e)
        {
            if (sender is ConnectionChatPanel pn && pn.Parent is FlowLayoutPanel flow)
            {
                try
                {
                    /*      var respond = _chatViewModel.GetCurrentConnectionActivate();

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
                    */

                    if (!_chatPresenter.ContainsSessionChat(pn.Name))
                    {
                        ProcessConnectionRemoved(pn.Id, pn.Name);
                        return;
                    }
                    foreach (var connection in flow.Controls.OfType<ConnectionChatPanel>())
                    {
                        connection.BackColor = DefaultColor;
                    }

                    _chatPresenter.SetActiveClientSession(pn.Name);

                    pn.BackColor = SelectedColor;
                    pn.ClearCount();
                    //Load chat history on current connection
                    //_chatViewModel.LoadChatHistoryByConnectionId(lb.Name);
                    ChangeChatDataByConnectionId(pn.Name);
                }
                catch(Exception ex)
                {
                    Logger.Log.ForContext("", LOG_FILE).Error(ex, "ChangeConnectionActivateEventHandler err ");
                }
            }
        }
        private void UpdateChatHistoryEventHandler(object sender, ChatUpdateChatHistoryEventArgs e)
        {
            InvokeAction(() =>
            {
                if (e.Messages == null || e.Messages.Length == 0)
                {
                    Log.ForContext("FileName", LOG_FILE).Error("Message is null or empty");
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
                        var name = _chatPresenter.GetNameBySessionId(e.ConnectionId);
                        if (!string.IsNullOrEmpty(name))
                            CreateMessageControl(name, chatMessage);
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

            if(_chatPresenter.GetActiveChatId(out string activeId))
                AddChatDataByConnectionId(activeId, lb);
        }
        private void BarUpdateEventHandler(object sender, ChatControlProgressBarUpdateUIEventArgs e)
        {
            try
            {
                var userChat = GetConnectionChatDataPanelByConnectionId(e.ConnectionId);

                if (userChat != null)
                {
                    InvokeAction(() => userChat.UpdateProgressBar(e.FileId, e.Status, e.Num));
                }
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("", LOG_FILE).Error(ex, "Progress bar update err ");
            }
        }
        private void AddedEventHandler(object sender, ChatControlAddedEventArgs e)
        {
            InvokeAction(() =>
            {
                switch (e.Type)
                {
                    case ChatControlType.Connection:
                        ProcessConnectionAdded(e.ConnectionId, e.Content);
                        RefreshUI(fpnNumberChatConnection);
                        break;
                    case ChatControlType.Message:
                        ProcessMessageAdded(e.ConnectionId, e.Content, e.ConnectionName);
                        RefreshUI(fpnChat);
                        break;
                    case ChatControlType.RequestAttachment:
                    case ChatControlType.ReceivedAttachment:
                        ProcessAttachmentAdded(e.Type, e.ConnectionId, e.FileInfo);
                        RefreshUI(fpnChat);
                        break;
                    default:
                        Logger.Log.ForContext("", LOG_FILE).Error("AddedEventHandler err: Unexpected event type ");
                        break;
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
            if(_chatPresenter.GetActiveChatId(out string activeId))
            {
                foreach (var item in chats)
                {
                    if (item.Name != activeId)
                        item.UpdateUnreadCount(1);
                }
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
                }
                else if (e.Type == ChatControlType.Message)
                {
                    ProcessMessageRemoved(e.ConnectionId, e.ControlKey);
                }
            });
        }
        private void ProcessConnectionAdded(string connectionId, string text)
        {
            ConnectionChatPanel lb = new ConnectionChatPanel(connectionId, text, fpnNumberChatConnection.Width - 2, 25);

            lb.Click += ActiveSessionChatChanged;

            fpnNumberChatConnection.Controls.Add(lb);

            ActiveSessionChatChanged(lb, EventArgs.Empty);
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
                ctl.Click -= ActiveSessionChatChanged;
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
            Action execute = () =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("", LOG_FILE).Error(ex, "Unhandled error in InvokeAction");
                }
            };

            if (this.InvokeRequired)
                this.Invoke((MethodInvoker)(() => execute()));
            else
                execute();
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

            if(_chatPresenter.GetActiveChatId(out string activeId))
            {
                foreach (var item in chats)
                {
                    if (item.Name != activeId)
                        item.UpdateUnreadCount(1);
                }
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
                if(_chatPresenter.SetFileSavePath(e.Attachment.Id, e.Path))
                {
                    var result = _chatPresenter.SaveConversation(e.Attachment.SocketId, e.Attachment.FileInfo.SavePath, e.Attachment.FileInfo.Filename, e.Attachment.FileInfo.FileSize);
                    if (result)
                    {
                        _chatPresenter.AcceptFile(e.Attachment.Id);
                    }
                }
            }
            //Refused file
            else if (e.Type == UserChatControlEventType.AttachmentRefused)
            {
                _chatPresenter.RejectFile(e.Attachment.Id);
            }
            //Cancel receive file
            else if (e.Type == UserChatControlEventType.AttachmentStopped)
            {
               _chatPresenter.CancelFileTransfer(e.Attachment.Id);
            }
        }
        private void RemoveChatByConnectionId(string connectionId)
        {
            _userChatControls.Remove(connectionId);

            foreach (Control item in fpnChat.Controls)
            {
                ProcessMessageRemoved(connectionId, item);
            }

            fpnChat.Controls.Clear();

            if(_chatPresenter.GetActiveChatId(out string activeId))
            {
                int index = fpnNumberChatConnection.Controls.IndexOfKey(activeId);
                if ((index >= 0 && index < fpnNumberChatConnection.Controls.Count))
                {
                    var lb = fpnNumberChatConnection.Controls[index];
                    ActiveSessionChatChanged(lb, EventArgs.Empty);

                    //ChangeChatDataByConnectionId(currentConnectionId);
                }
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
