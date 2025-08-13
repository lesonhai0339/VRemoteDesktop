using System;
using System.Windows.Forms;

namespace VRemoteDesktop
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
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.lbStatus = new System.Windows.Forms.Label();
            this.pnStatus = new System.Windows.Forms.Panel();
            this.btnConnect = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtPartnerId = new System.Windows.Forms.TextBox();
            this.txtPartnerPassword = new System.Windows.Forms.TextBox();
            this.txtOwnerPassword = new System.Windows.Forms.TextBox();
            this.txtOwnerId = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(203, 103);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(61, 16);
            this.label7.TabIndex = 25;
            this.label7.Text = "Mật khẩu";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(203, 50);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(18, 16);
            this.label6.TabIndex = 24;
            this.label6.Text = "Id";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(9, 103);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(61, 16);
            this.label5.TabIndex = 23;
            this.label5.Text = "Mật khẩu";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(9, 50);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(18, 16);
            this.label4.TabIndex = 22;
            this.label4.Text = "Id";
            // 
            // lbStatus
            // 
            this.lbStatus.AutoSize = true;
            this.lbStatus.Location = new System.Drawing.Point(26, 185);
            this.lbStatus.Name = "lbStatus";
            this.lbStatus.Size = new System.Drawing.Size(96, 16);
            this.lbStatus.TabIndex = 21;
            this.lbStatus.Text = "Chưa sẵn sàng";
            // 
            // pnStatus
            // 
            this.pnStatus.Location = new System.Drawing.Point(5, 185);
            this.pnStatus.Name = "pnStatus";
            this.pnStatus.Size = new System.Drawing.Size(15, 15);
            this.pnStatus.TabIndex = 20;
            this.pnStatus.Paint += new PaintEventHandler(pnStatus_Paint);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(253, 166);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(103, 35);
            this.btnConnect.TabIndex = 19;
            this.btnConnect.Text = "Kết nối";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(203, 17);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(55, 18);
            this.label2.TabIndex = 18;
            this.label2.Text = "Khách";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(9, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(37, 18);
            this.label1.TabIndex = 15;
            this.label1.Text = "Bạn";
            // 
            // txtPartnerId
            // 
            this.txtPartnerId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPartnerId.Location = new System.Drawing.Point(206, 69);
            this.txtPartnerId.Name = "txtPartnerId";
            this.txtPartnerId.Size = new System.Drawing.Size(150, 22);
            this.txtPartnerId.TabIndex = 13;
            // 
            // txtPartnerPassword
            // 
            this.txtPartnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPartnerPassword.Location = new System.Drawing.Point(206, 128);
            this.txtPartnerPassword.Name = "txtPartnerPassword";
            this.txtPartnerPassword.Size = new System.Drawing.Size(150, 22);
            this.txtPartnerPassword.TabIndex = 14;
            // 
            // txtOwnerPassword
            // 
            this.txtOwnerPassword.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOwnerPassword.Location = new System.Drawing.Point(12, 128);
            this.txtOwnerPassword.Name = "txtOwnerPassword";
            this.txtOwnerPassword.ReadOnly = true;
            this.txtOwnerPassword.Size = new System.Drawing.Size(150, 22);
            this.txtOwnerPassword.TabIndex = 17;
            this.txtOwnerPassword.TabStop = false;
            // 
            // txtOwnerId
            // 
            this.txtOwnerId.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOwnerId.Location = new System.Drawing.Point(12, 69);
            this.txtOwnerId.Name = "txtOwnerId";
            this.txtOwnerId.ReadOnly = true;
            this.txtOwnerId.Size = new System.Drawing.Size(150, 22);
            this.txtOwnerId.TabIndex = 16;
            this.txtOwnerId.TabStop = false;

            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(368, 218);
            this.MaximizeBox = false;
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.lbStatus);
            this.Controls.Add(this.pnStatus);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtPartnerId);
            this.Controls.Add(this.txtPartnerPassword);
            this.Controls.Add(this.txtOwnerPassword);
            this.Controls.Add(this.txtOwnerId);
            this.Name = "FormMain";
            this.Text = "FormMain";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.Shown += new System.EventHandler(this.FormMain_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }



        #endregion

        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label lbStatus;
        private System.Windows.Forms.Panel pnStatus;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtPartnerId;
        private System.Windows.Forms.TextBox txtPartnerPassword;
        private System.Windows.Forms.TextBox txtOwnerPassword;
        private System.Windows.Forms.TextBox txtOwnerId;
    }
}

