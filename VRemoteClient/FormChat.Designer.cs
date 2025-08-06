namespace VRemoteClient
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
            this.txtChatContent = new System.Windows.Forms.TextBox();
            this.btnSend = new System.Windows.Forms.Button();
            this.btnSendAttachment = new System.Windows.Forms.Button();
            this.fpnChat = new System.Windows.Forms.FlowLayoutPanel();
            this.SuspendLayout();
            // 
            // txtChatContent
            // 
            this.txtChatContent.Location = new System.Drawing.Point(12, 355);
            this.txtChatContent.Name = "txtChatContent";
            this.txtChatContent.Size = new System.Drawing.Size(208, 22);
            this.txtChatContent.TabIndex = 0;
            // 
            // btnSend
            // 
            this.btnSend.Location = new System.Drawing.Point(268, 343);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 35);
            this.btnSend.TabIndex = 1;
            this.btnSend.Text = "Gửi";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.button1_Click);
            // 
            // btnSendAttachment
            // 
            this.btnSendAttachment.Image = ((System.Drawing.Image)(resources.GetObject("btnSendAttachment.Image")));
            this.btnSendAttachment.Location = new System.Drawing.Point(226, 355);
            this.btnSendAttachment.Name = "btnSendAttachment";
            this.btnSendAttachment.Size = new System.Drawing.Size(34, 23);
            this.btnSendAttachment.TabIndex = 2;
            this.btnSendAttachment.UseMnemonic = false;
            this.btnSendAttachment.UseVisualStyleBackColor = true;
            // 
            // fpnChat
            // 
            this.fpnChat.Location = new System.Drawing.Point(12, 13);
            this.fpnChat.Name = "fpnChat";
            this.fpnChat.Size = new System.Drawing.Size(331, 324);
            this.fpnChat.TabIndex = 3;
            // 
            // FormChat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(355, 388);
            this.Controls.Add(this.fpnChat);
            this.Controls.Add(this.btnSendAttachment);
            this.Controls.Add(this.btnSend);
            this.Controls.Add(this.txtChatContent);
            this.Name = "FormChat";
            this.Text = "FormChat";
            this.Load += new System.EventHandler(this.FormChat_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtChatContent;
        private System.Windows.Forms.Button btnSend;
        private System.Windows.Forms.Button btnSendAttachment;
        private System.Windows.Forms.FlowLayoutPanel fpnChat;
    }
}