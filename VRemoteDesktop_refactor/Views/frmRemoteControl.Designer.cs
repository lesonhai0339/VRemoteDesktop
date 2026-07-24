namespace Vsign4.VRemoteDesktop.Views
{
    partial class frmRemoteControl
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmRemoteControl));
            this.vPictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // vPictureBox
            // 
            resources.ApplyResources(this.vPictureBox, "vPictureBox");
            this.vPictureBox.Name = "vPictureBox";
            this.vPictureBox.TabStop = false;
            // 
            // frmRemoteControl
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.vPictureBox);
            this.Name = "frmRemoteControl";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmRemoteControl_FormClosing);
            this.Load += new System.EventHandler(this.frmRemoteControl_Load);
            this.Shown += new System.EventHandler(this.frmRemoteControl_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox vPictureBox;
    }
}