using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomMessage : UserControl
    {
        private FlowLayoutPanel flowLayoutPanel1;
        public CustomMessage(ChatMessageType type,Size size, object content)
        {
            InitializeComponent(size);
            MessageGenerate(type, content);
        }
        private void InitializeComponent(Size size)
        {
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(size.Width - 15, 40);
            this.flowLayoutPanel1.TabIndex = 2;
            this.flowLayoutPanel1.AutoSize = true;
            // 
            // CustomMessage
            // 
            this.Controls.Add(this.flowLayoutPanel1);
            this.Name = "CustomMessage";
            this.Size = new System.Drawing.Size(size.Width -10, 40);
            this.ResumeLayout(false);

        }
        private void MessageGenerate(ChatMessageType type, object content)
        {
            switch (type)
            {
                case ChatMessageType.TEXT:
                    var tb = GenerateTextElement(content.ToString());
                    AddElementToLayout(tb);
                    break;
                case ChatMessageType.FILE:
                    AddFileToList(content.ToString());
                    break;
                default:
                    break;
            }
        }
        private void AddFileToList(string filePath)
        {
            FileInfo fileInfo = new FileInfo(filePath);
            Icon icon = Icon.ExtractAssociatedIcon(filePath);

            PictureBox picturebox = new PictureBox();
            picturebox.Image = icon.ToBitmap();

            Label fileName = new Label();
            Label fileSize= new Label();
            fileName.Text = fileInfo.FullName;
            fileSize.Text = fileInfo.Length.ToString() ;

            Button open = new Button
            {
                Name = "btnOpen",
                Text = "Open",
                BackColor = Color.White

            };

            Button save = new Button
            {
                Name = "btnSave",
                Text = "Save",
                BackColor = Color.White
            };
            flowLayoutPanel1.Controls.Add(picturebox);
            flowLayoutPanel1.Controls.Add(fileName);
            flowLayoutPanel1.Controls.Add(fileSize);
            flowLayoutPanel1.Controls.Add(open);
            flowLayoutPanel1.Controls.Add(save);
        }
        private Control GenerateTextElement(string text)
        {
            TextBox tb = new TextBox
            {
                Text =text,
                ReadOnly = true,
                BorderStyle = 0,
                BackColor = System.Drawing.Color.White,
                TabStop = false,
                Width = flowLayoutPanel1.Width - 20,
                WordWrap = true,
                ScrollBars = ScrollBars.None,
                AcceptsReturn = true,
                Multiline = true
            };
            return tb;
        }
        private void AddElementToLayout(Control control)
        {
            if (control != null)
            {
                this.flowLayoutPanel1.Controls.Add(control);
            }
        }
    }
}
