using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Presenters.Enums;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;
using Vsign4.VRemoteDesktop.Services.FileService.Enums;

namespace Vsign4.VRemoteDesktop.Layouts
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
                WrapContents = false,
            };
            this.Controls.Add(_messagePanel);
            _connectionId = connectionId;
        }
        public void AddControl(Control control)
        {
            this._messagePanel.Controls.Add(control);
            this._messagePanel.ScrollControlIntoView(control);
        }
        public void RemoveControlByKey(string key)
        {
            foreach(Control ctl in this.Controls)
            {
                FileAttachmentPanel file = ctl as FileAttachmentPanel;  
                if (file != null)
                {
                    file.AcceptSaveFile += ProcessAttachmentRespondFromPartner;
                    _messagePanel.Controls.Remove(file);
                    file.Dispose();
                }
                Label lb = ctl as Label;
                if (lb != null)
                {
                    _messagePanel.Controls.Remove(lb);
                    lb.Dispose();
                }
            }
        }
        public void RemoveControlByKey(Control control)
        {
            FileAttachmentPanel file = control as FileAttachmentPanel;      
            if (file != null && _messagePanel.Contains(file))
            {
                file.AcceptSaveFile += ProcessAttachmentRespondFromPartner;
                _messagePanel.Controls.Remove(file);
                file.Dispose();
            }

            Label lb = control as Label;    
            if (lb != null && _messagePanel.Contains(lb))
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
        private void ProcessAttachmentRespondFromPartner(object sender , FileReceivedEventArgs e)
        {
            Button btn = sender as Button;
            FileAttachmentPanel attachment = (btn.Parent == null) ? null : btn.Parent as FileAttachmentPanel;   
            if (btn != null && attachment != null)
            {
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    attachment.AcceptSendFile();

                    if(UserChatEvent != null)
                        UserChatEvent.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentAccepted, attachment, e.FilePath));
                }
                else if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    if (UserChatEvent != null)
                        UserChatEvent.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentRefused, attachment, null));

                    attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                    attachment.RejectSendFile();
                    _attachments.Remove(attachment.Id);  
                }
                else if (string.Compare(btn.Name, "btnStop") == 0)
                {
                    if (UserChatEvent != null)
                        UserChatEvent.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentStopped, attachment, null));
                    
                    attachment.DisableControl(btn);
                    attachment.RemoveProgressBar();
                    attachment.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
                    _attachments.Remove(attachment.Id);
                }
            }
        }
        public void UpdateProgressBar(string fileId, FileStatus status, int num)
        {
            FileAttachmentPanel attachment;
            if (_attachments.TryGetValue(fileId, out attachment))
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
            FileAttachmentPanel attachment;
            if (_attachments.TryGetValue(fileId, out attachment))
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
