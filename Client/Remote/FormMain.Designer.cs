
using System;

namespace RemoteClient.Remote
{
    partial class FormMain
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
            this.txtPartnerId = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.txtYourId = new System.Windows.Forms.TextBox();
            this.txtYourPwd = new System.Windows.Forms.TextBox();
            this.txtPartnerPwd = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lbConnectStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtPartnerId
            // 
            this.txtPartnerId.Location = new System.Drawing.Point(235, 47);
            this.txtPartnerId.Margin = new System.Windows.Forms.Padding(4);
            this.txtPartnerId.Name = "txtPartnerId";
            this.txtPartnerId.Size = new System.Drawing.Size(149, 22);
            this.txtPartnerId.TabIndex = 0;
            // 
            // button1
            // 
            this.button1.FlatAppearance.BorderColor = System.Drawing.Color.Aqua;
            this.button1.Location = new System.Drawing.Point(396, 160);
            this.button1.Margin = new System.Windows.Forms.Padding(4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(100, 28);
            this.button1.TabIndex = 1;
            this.button1.Text = "Kết nối";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // txtYourId
            // 
            this.txtYourId.Enabled = false;
            this.txtYourId.Location = new System.Drawing.Point(20, 47);
            this.txtYourId.Margin = new System.Windows.Forms.Padding(4);
            this.txtYourId.Name = "txtYourId";
            this.txtYourId.Size = new System.Drawing.Size(156, 22);
            this.txtYourId.TabIndex = 2;
            // 
            // txtYourPwd
            // 
            this.txtYourPwd.Enabled = false;
            this.txtYourPwd.Location = new System.Drawing.Point(20, 112);
            this.txtYourPwd.Margin = new System.Windows.Forms.Padding(4);
            this.txtYourPwd.Name = "txtYourPwd";
            this.txtYourPwd.Size = new System.Drawing.Size(156, 22);
            this.txtYourPwd.TabIndex = 3;
            // 
            // txtPartnerPwd
            // 
            this.txtPartnerPwd.Location = new System.Drawing.Point(235, 112);
            this.txtPartnerPwd.Margin = new System.Windows.Forms.Padding(4);
            this.txtPartnerPwd.Name = "txtPartnerPwd";
            this.txtPartnerPwd.Size = new System.Drawing.Size(149, 22);
            this.txtPartnerPwd.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 27);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 16);
            this.label1.TabIndex = 5;
            this.label1.Text = "Id của bạn";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(231, 27);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(63, 16);
            this.label2.TabIndex = 6;
            this.label2.Text = "ID đối tác";
            // 
            // panel1
            // 
            this.panel1.Location = new System.Drawing.Point(12, 173);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(15, 15);
            this.panel1.TabIndex = 7;
            this.panel1.Paint += new System.Windows.Forms.PaintEventHandler(this.panel1_Paint_1);
            // 
            // lbConnectStatus
            // 
            this.lbConnectStatus.AutoSize = true;
            this.lbConnectStatus.Location = new System.Drawing.Point(33, 173);
            this.lbConnectStatus.Name = "lbConnectStatus";
            this.lbConnectStatus.Size = new System.Drawing.Size(0, 16);
            this.lbConnectStatus.TabIndex = 8;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(512, 203);
            this.Controls.Add(this.lbConnectStatus);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtPartnerPwd);
            this.Controls.Add(this.txtYourPwd);
            this.Controls.Add(this.txtYourId);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.txtPartnerId);
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "FormMain";
            this.Text = "FormMain";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtPartnerId;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TextBox txtYourId;
        private System.Windows.Forms.TextBox txtYourPwd;
        private System.Windows.Forms.TextBox txtPartnerPwd;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label lbConnectStatus;
    }
}