using System.Windows.Forms;
using System.Drawing;

namespace Vsign4.VRemoteDesktop.Views
{
    partial class frmRemote
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmRemote));
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.lbStatus = new System.Windows.Forms.Label();
            this.pnStatus = new System.Windows.Forms.Panel();
            this.btnConnect = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.txtPartnerPassword = new System.Windows.Forms.TextBox();
            this.txtOwnerPassword = new System.Windows.Forms.TextBox();
            this.txtOwnerId = new System.Windows.Forms.TextBox();
            this.txtDefaultPassword = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnSetDefaultPassword = new System.Windows.Forms.Button();
            this.lbP2PConnectStatus = new System.Windows.Forms.Label();
            this.listViewControl1 = new Vsign4.VRemoteDesktop.Layouts.ListViewControl();
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
            this.label1.Click += new System.EventHandler(this.label1_Click);
            // 
            // txtPartnerPassword
            // 
            this.txtPartnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            resources.ApplyResources(this.txtPartnerPassword, "txtPartnerPassword");
            this.txtPartnerPassword.Name = "txtPartnerPassword";
            // 
            // txtOwnerPassword
            // 
            this.txtOwnerPassword.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerPassword.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            resources.ApplyResources(this.txtOwnerPassword, "txtOwnerPassword");
            this.txtOwnerPassword.Name = "txtOwnerPassword";
            this.txtOwnerPassword.ReadOnly = true;
            this.txtOwnerPassword.TabStop = false;
            // 
            // txtOwnerId
            // 
            this.txtOwnerId.BackColor = System.Drawing.SystemColors.ControlLightLight;
            this.txtOwnerId.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            resources.ApplyResources(this.txtOwnerId, "txtOwnerId");
            this.txtOwnerId.Name = "txtOwnerId";
            this.txtOwnerId.ReadOnly = true;
            this.txtOwnerId.TabStop = false;
            // 
            // txtDefaultPassword
            // 
            this.txtDefaultPassword.BackColor = System.Drawing.Color.White;
            resources.ApplyResources(this.txtDefaultPassword, "txtDefaultPassword");
            this.txtDefaultPassword.Name = "txtDefaultPassword";
            this.txtDefaultPassword.ReadOnly = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // btnSetDefaultPassword
            // 
            this.btnSetDefaultPassword.Image = global::Vsign4.Properties.Resources.key_16px;
            resources.ApplyResources(this.btnSetDefaultPassword, "btnSetDefaultPassword");
            this.btnSetDefaultPassword.Name = "btnSetDefaultPassword";
            this.btnSetDefaultPassword.UseVisualStyleBackColor = true;
            this.btnSetDefaultPassword.Click += new System.EventHandler(this.btnSetDefaultPassword_Click);
            // 
            // lbP2PConnectStatus
            // 
            resources.ApplyResources(this.lbP2PConnectStatus, "lbP2PConnectStatus");
            this.lbP2PConnectStatus.Name = "lbP2PConnectStatus";
            // 
            // listViewControl1
            // 
            this.listViewControl1.BackColor = System.Drawing.Color.White;
            this.listViewControl1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            resources.ApplyResources(this.listViewControl1, "listViewControl1");
            this.listViewControl1.Name = "listViewControl1";
            // 
            // frmRemote
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lbP2PConnectStatus);
            this.Controls.Add(this.listViewControl1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnSetDefaultPassword);
            this.Controls.Add(this.txtDefaultPassword);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.lbStatus);
            this.Controls.Add(this.pnStatus);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.txtPartnerPassword);
            this.Controls.Add(this.txtOwnerPassword);
            this.Controls.Add(this.txtOwnerId);
            this.Cursor = System.Windows.Forms.Cursors.Default;
            this.Name = "frmRemote";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmRemote_FormClosing);
            this.Load += new System.EventHandler(this.frmRemote_Load);
            this.Shown += new System.EventHandler(this.frmRemote_Shown);
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
        private System.Windows.Forms.TextBox txtPartnerPassword;
        private System.Windows.Forms.TextBox txtOwnerPassword;
        private System.Windows.Forms.TextBox txtOwnerId;
        private TextBox txtDefaultPassword;
        private Button btnSetDefaultPassword;
        private Label label3;
        private VRemoteDesktop.Layouts.ListViewControl listViewControl1;
        private Label lbP2PConnectStatus;
    }
}