using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TextBox;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Layouts
{
    public class ConnectionChatDataPanel: UserControl
    {
        private string _connectionId;
        private FlowLayoutPanel _messagePanel;
        private Dictionary<string, FileAttachmentPanel> _attachments;
        public event EventHandler<UserChatControlEventArgs> UserChatEvent;
        public ConnectionChatDataPanel(string connectionId)
        {
            _attachments = new Dictionary<string, FileAttachmentPanel>();
            _messagePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            this.Controls.Add(_messagePanel);
            _connectionId = connectionId;
        }
        public void AddControl(Control control)
        {
            this._messagePanel.Controls.Add(control);
            this._messagePanel.ScrollControlIntoView(control);
            Console.WriteLine(_messagePanel.Controls.Count);
        }
        public void RemoveControlByKey(string key)
        {
            foreach(Control ctl in this.Controls)
            {
                if (ctl is FileAttachmentPanel file)
                {
                    file.AcceptSaveFile += ProcessAttachmentRespondFromPartner;
                    _messagePanel.Controls.Remove(file);
                    file.Dispose();
                }
                else if (ctl is Label lb)
                {
                    _messagePanel.Controls.Remove(lb);
                    lb.Dispose();
                }
            }
        }
        public void RemoveControlByKey(Control control)
        {
            if (control is FileAttachmentPanel file && _messagePanel.Contains(file))
            {
                file.AcceptSaveFile += ProcessAttachmentRespondFromPartner;
                _messagePanel.Controls.Remove(file);
                file.Dispose();
            }
            else if (control is Label lb && _messagePanel.Contains(lb))
            {
                _messagePanel.Controls.Remove(lb);
                lb.Dispose();
            }
        }
        public void AddAttachment(ChatControlType type, string connectionId, VFileInfo fileInfo)
        {
            FileAttachmentPanel attachment = new FileAttachmentPanel(fileInfo.Id, connectionId);
            if (type == ChatControlType.RequestAttachment)
            {
                attachment.Add(fileInfo, true);
                _attachments.Add(fileInfo.Id, attachment);
            }
            if (type == ChatControlType.ReceivedAttachment)
            {
                attachment.Add(fileInfo, false);
                _attachments[fileInfo.Id] = attachment;
                attachment.AcceptSaveFile += ProcessAttachmentRespondFromPartner;
            }
            this._messagePanel.Controls.Add(attachment);
            this._messagePanel.ScrollControlIntoView(attachment);
        }
        private void ProcessAttachmentRespondFromPartner(object sender , P2PFileReceivedEventArgs e)
        {
            if (sender is Button btn && btn.Parent is FileAttachmentPanel attachment)
            {
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    attachment.AcceptSendFile();
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentAccepted, attachment, e.FilePath));
                }
                else if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentRefused, attachment, null));

                    attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                    attachment.RejectSendFile();
                    _attachments.Remove(attachment.Id);  
                }
                else if (string.Compare(btn.Name, "btnStop") == 0)
                {
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentStopped, attachment, null));
                    attachment.DisableControl(btn);
                    attachment.RemoveProgressBar();
                    attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                    _attachments.Remove(attachment.Id);
                }
            }
        }
        public void UpdateProgressBar(string fileId, FileStatus status, int num)
        {
            if (_attachments.TryGetValue(fileId, out var attachment))
            {
                if (status == FileStatus.CheckSumFailed)
                {
                    attachment.UpdateRequestSendFileStatus("File lỗi");
                    attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                    _attachments.Remove(fileId);
                }
                else
                {
                    if (status == FileStatus.Finished)
                    {
                        attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                        _attachments.Remove(fileId);
                    }

                    attachment.UpdateProgressBar(num);
                }
            }
        }
        public void UpdateAttachmentStatus(string fileId, ChatControlType type)
        {
            if (_attachments.TryGetValue(fileId, out var attachment))
            {
                if (type == ChatControlType.AcceptAttachment)
                    attachment.UpdateRequestSendFileStatus("Đối tác đã chấp nhận");

                else if (type == ChatControlType.RefuseAttachment)
                    attachment.UpdateRequestSendFileStatus("Đối tác đã từ chối");

                else if (type == ChatControlType.StopSendingAttachment)
                    attachment.UpdateRequestSendFileStatus("Đối tác hủy nhận file");
            }
        }
        protected override void Dispose(bool disposing)
        {
            foreach(var attachment in _attachments)
            {
                attachment.Value.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
            }
            _attachments.Clear();
            base.Dispose(disposing);
        }
    }
}
