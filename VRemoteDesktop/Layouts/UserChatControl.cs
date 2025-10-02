using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.ViewModels;
using VRemoteDesktop.Services.FileService;
using static System.Net.WebRequestMethods;
using VRemoteDesktop.Helpers;
using System.Drawing;

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
            Console.WriteLine(_messagePanel.Controls.Count);

        }
        private void ProcessAttachmentRespondFromPartner(object sender , P2PFileReceivedEventArgs e)
        {
            if (sender is Button btn && btn.Parent is FileAttachmentLayout parent)
            {
                //Accept file
                if (string.Compare(btn.Name, "btnSave") == 0)
                {
                    FileAttachment.UpdateFileSavePath(parent.Id, e.FilePath);
                    byte[] data = ByteArrayHelper.ConvertStringToByteArray(parent.Id, EncodingType.ASCII).GetResult();
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentAccepted, parent.Id, data));
                }
                //Reject file
                else if (string.Compare(btn.Name, "btnCancel") == 0)
                {
                    FileAttachment.RemoveFileInfo(parent.Id);
                    byte[] data = ByteArrayHelper.ConvertStringToByteArray(parent.Id, EncodingType.ASCII).GetResult();
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentRefused, parent.Id, data));
                }
                //Stop receiving
                else if (string.Compare(btn.Name, "btnStop") == 0)
                {
                    FileAttachment.CleanUpFileInfo(parent.Id);
                    byte[] data = ByteArrayHelper.ConvertStringToByteArray(parent.Id, EncodingType.ASCII).GetResult();
                    UserChatEvent?.Invoke(this, new UserChatControlEventArgs(UserChatControlEventType.AttachmentStopped, parent.Id, data));
                }
            }
        }
        protected override void Dispose(bool disposing)
        {
            foreach(var attachment in _attachments)
            {
                attachment.Value.AcceptSaveFile -= ProcessAttachmentRespondFromPartner;
            }
            base.Dispose(disposing);
        }
    }
}
