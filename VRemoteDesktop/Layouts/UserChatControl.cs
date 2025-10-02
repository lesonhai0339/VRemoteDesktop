using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Layouts
{
    public class UserChatControl: UserControl
    {
        private string _connectionId;
        private FlowLayoutPanel _messagePanel;
        private Dictionary<string, FileAttachmentLayout> _attachments;
        public event EventHandler<UserChatControlEventArgs> UserChatEvent;
        public UserChatControl(string connectionId)
        {
            _attachments = new Dictionary<string, FileAttachmentLayout>();
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
        public void AddAttachment(ChatControlType type, string connectionId, VFileInfo fileInfo)
        {
            FileAttachmentLayout attachment = new FileAttachmentLayout(fileInfo.Id, connectionId);
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
            if (sender is Button btn && btn.Parent is FileAttachmentLayout parent)
            {
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentAccepted, parent, e.FilePath));
                }
                else if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentRefused, parent, null));

                    parent.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;

                    _messagePanel.Controls.Remove(parent);
                    parent?.Dispose();
                }
                else if (string.Compare(btn.Name, "btnStop") == 0)
                {
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentStopped, parent, null));


                    parent.DisableControl(btn);
                    parent.RemoveProgressBar();
                    parent.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;

                    _messagePanel.Controls.Remove(parent);
                    parent?.Dispose();
                }
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
