using System;
using System.Windows.Forms;

namespace VRemoteClient
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
            this.vPictureBox = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox)).BeginInit();
            this.SuspendLayout();
            // 
            // vPictureBox
            // 
            this.vPictureBox.Location = new System.Drawing.Point(13, 13);
            this.vPictureBox.Name = "vPictureBox";
            this.vPictureBox.Size = new System.Drawing.Size(775, 425);
            this.vPictureBox.TabIndex = 0;
            this.vPictureBox.TabStop = false;
            // 
            // FormRemote
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.vPictureBox);
            this.Name = "FormRemote";
            this.Text = "FormRemote";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormRemote_FormClosing);
            this.Load += new System.EventHandler(this.FormRemote_Load);
            this.Shown += new System.EventHandler(this.FormRemote_Shown);
            ((System.ComponentModel.ISupportInitialize)(this.vPictureBox)).EndInit();
            this.ResumeLayout(false);

        }




        #endregion

        private System.Windows.Forms.PictureBox vPictureBox;
    }
}