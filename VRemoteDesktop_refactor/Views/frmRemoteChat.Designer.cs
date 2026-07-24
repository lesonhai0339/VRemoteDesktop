using System;
using System.Drawing;
using System.Windows.Forms;

namespace Vsign4.VRemoteDesktop.Views
{
    partial class frmRemoteChat
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmRemoteChat));
            this.fpnNumberChatConnection = new System.Windows.Forms.FlowLayoutPanel();
            this.fpnChat = new System.Windows.Forms.FlowLayoutPanel();
            this.btnSendAttachment = new System.Windows.Forms.Button();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtChatContent = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // fpnNumberChatConnection
            // 
            resources.ApplyResources(this.fpnNumberChatConnection, "fpnNumberChatConnection");
            this.fpnNumberChatConnection.BackColor = System.Drawing.Color.White;
            this.fpnNumberChatConnection.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.fpnNumberChatConnection.Name = "fpnNumberChatConnection";
            // 
            // fpnChat
            // 
            resources.ApplyResources(this.fpnChat, "fpnChat");
            this.fpnChat.BackColor = System.Drawing.Color.White;
            this.fpnChat.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.fpnChat.Name = "fpnChat";
            // 
            // btnSendAttachment
            // 
            resources.ApplyResources(this.btnSendAttachment, "btnSendAttachment");
            this.btnSendAttachment.Name = "btnSendAttachment";
            this.btnSendAttachment.UseMnemonic = false;
            this.btnSendAttachment.UseVisualStyleBackColor = true;
            this.btnSendAttachment.Click += new System.EventHandler(this.btnSendAttachment_Click);
            // 
            // btnSend
            // 
            resources.ApplyResources(this.btnSend, "btnSend");
            this.btnSend.Name = "btnSend";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // txtChatContent
            // 
            resources.ApplyResources(this.txtChatContent, "txtChatContent");
            this.txtChatContent.Name = "txtChatContent";
            // 
            // frmRemoteChat
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.fpnNumberChatConnection);
            this.Controls.Add(this.fpnChat);
            this.Controls.Add(this.btnSendAttachment);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtChatContent);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmRemoteChat";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmRemoteChat_FormClosing);
            this.Load += new System.EventHandler(this.frmRemoteChat_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }



        #endregion

        private System.Windows.Forms.FlowLayoutPanel fpnNumberChatConnection;
        private System.Windows.Forms.FlowLayoutPanel fpnChat;
        private System.Windows.Forms.Button btnSendAttachment;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.TextBox txtChatContent;
    }
}