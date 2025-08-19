namespace VRemoteDesktop.Views
{
    partial class FormChat
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormChat));
            this.fpnNumberChatConnection = new System.Windows.Forms.FlowLayoutPanel();
            this.fpnChat = new System.Windows.Forms.FlowLayoutPanel();
            this.btnSendAttachment = new System.Windows.Forms.Button();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtChatContent = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // fpnNumberChatConnection
            // 
            this.fpnNumberChatConnection.Location = new System.Drawing.Point(21, 21);
            this.fpnNumberChatConnection.Name = "fpnNumberChatConnection";
            this.fpnNumberChatConnection.Size = new System.Drawing.Size(330, 98);
            this.fpnNumberChatConnection.TabIndex = 9;
            // 
            // fpnChat
            // 
            this.fpnChat.Location = new System.Drawing.Point(20, 125);
            this.fpnChat.Name = "fpnChat";
            this.fpnChat.Size = new System.Drawing.Size(331, 220);
            this.fpnChat.TabIndex = 8;
            // 
            // btnSendAttachment
            // 
            this.btnSendAttachment.Image = ((System.Drawing.Image)(resources.GetObject("btnSendAttachment.Image")));
            this.btnSendAttachment.Location = new System.Drawing.Point(234, 363);
            this.btnSendAttachment.Name = "btnSendAttachment";
            this.btnSendAttachment.Size = new System.Drawing.Size(34, 23);
            this.btnSendAttachment.TabIndex = 7;
            this.btnSendAttachment.UseMnemonic = false;
            this.btnSendAttachment.UseVisualStyleBackColor = true;
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(276, 351);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 35);
            this.btnSend.TabIndex = 6;
            this.btnSend.Text = "Gửi";
            this.btnSend.UseVisualStyleBackColor = true;
            // 
            // txtChatContent
            // 
            this.txtChatContent.Font = new System.Drawing.Font("Arial", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtChatContent.Location = new System.Drawing.Point(20, 363);
            this.txtChatContent.Name = "txtChatContent";
            this.txtChatContent.Size = new System.Drawing.Size(208, 25);
            this.txtChatContent.TabIndex = 5;
            // 
            // FormChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(371, 408);
            this.Controls.Add(this.fpnNumberChatConnection);
            this.Controls.Add(this.fpnChat);
            this.Controls.Add(this.btnSendAttachment);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtChatContent);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "FormChat";
            this.Text = "FormChat";
            this.Load += new System.EventHandler(this.FormChat_Load);
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