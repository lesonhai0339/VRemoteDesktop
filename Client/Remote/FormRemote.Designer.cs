using System;

namespace RemoteClient.Remote
{
    partial class FormRemote
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
            this.vPictureBox1 = new RemoteClient.Remote.VPictureBox();
            this.vKeyboardReceived1 = new RemoteClient.Remote.VKeyboardReceived();
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // vPictureBox1
            // 
            this.vPictureBox1.Location = new System.Drawing.Point(12, 12);
            this.vPictureBox1.Name = "vPictureBox1";
            this.vPictureBox1.Size = new System.Drawing.Size(776, 426);
            this.vPictureBox1.TabIndex = 0;
            this.vPictureBox1.TabStop = false;
            // 
            // vKeyboardReceived1
            // 
            this.vKeyboardReceived1.Location = new System.Drawing.Point(788, 444);
            this.vKeyboardReceived1.Name = "vKeyboardReceived1";
            this.vKeyboardReceived1.Size = new System.Drawing.Size(10, 10);
            this.vKeyboardReceived1.TabIndex = 1;
            // 
            // FormRemote
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.vKeyboardReceived1);
            this.Controls.Add(this.vPictureBox1);
            this.Name = "FormRemote";
            this.Text = "FormRemote";
            this.Load += new System.EventHandler(this.FormRemote_Load);
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private VPictureBox vPictureBox1;
        private VKeyboardReceived vKeyboardReceived1;
    }
}