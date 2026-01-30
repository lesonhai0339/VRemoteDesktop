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
            System.ComponentModel.ComponentResourceManager resources = 
                new System.ComponentModel.ComponentResourceManager(typeof(FormMain));

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
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // lbStatus
            // 
            resources.ApplyResources(this.lbStatus, "lbStatus");
            this.lbStatus.Name = "lbStatus";
            // 
            // pnStatus
            // 
            resources.ApplyResources(this.pnStatus, "pnStatus");
            this.pnStatus.Name = "pnStatus";
            this.pnStatus.Paint += new System.Windows.Forms.PaintEventHandler(this.pnStatus_Paint);
            // 
            // btnConnect
            // 
            resources.ApplyResources(this.btnConnect, "btnConnect");
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // txtPartnerId
            // 
            resources.ApplyResources(this.txtPartnerId, "txtPartnerId");
            this.txtPartnerId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPartnerId.Name = "txtPartnerId";
            // 
            // txtPartnerPassword
            // 
            resources.ApplyResources(this.txtPartnerPassword, "txtPartnerPassword");
            this.txtPartnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtPartnerPassword.Name = "txtPartnerPassword";
            // 
            // txtOwnerPassword
            // 
            resources.ApplyResources(this.txtOwnerPassword, "txtOwnerPassword");
            this.txtOwnerPassword.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOwnerPassword.Name = "txtOwnerPassword";
            this.txtOwnerPassword.ReadOnly = true;
            this.txtOwnerPassword.TabStop = false;
            // 
            // txtOwnerId
            // 
            resources.ApplyResources(this.txtOwnerId, "txtOwnerId");
            this.txtOwnerId.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtOwnerId.Name = "txtOwnerId";
            this.txtOwnerId.ReadOnly = true;
            this.txtOwnerId.TabStop = false;
            // 
            // FormMain
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
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
            this.MaximizeBox = false;
            this.Name = "FormMain";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormMain_FormClosing);
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

