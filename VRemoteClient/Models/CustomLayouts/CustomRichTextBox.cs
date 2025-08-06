using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteClient.Models.CustomLayouts
{
    public class CustomRichTextBox: RichTextBox
    {
        private static readonly Font BoldFont = new Font("Arial", 9, FontStyle.Bold);
        private static readonly Font RegularFont = new Font("Arial", 8, FontStyle.Regular);
        public CustomRichTextBox()
        {
            this.BorderStyle = BorderStyle.None;
            this.ReadOnly = true;
            this.BackColor = SystemColors.Window;
            this.ScrollBars = RichTextBoxScrollBars.None;
            this.Multiline = true;
            this.WordWrap = true;
            this.AutoSize = true;

        }
        public CustomRichTextBox SetAutoHeight(int maxWidth)
        {
            // Set the width first
            this.Width = maxWidth;

            //// Calculate preferred height based on content
            //Size preferredSize = this.GetPreferredSize(new Size(maxWidth, int.MaxValue));
            //this.Height = preferredSize.Height;

            return this;
        }
        public CustomRichTextBox SetMargin(int margin)
        {
            this.Margin = new Padding(margin);
            return this;
        }
        public CustomRichTextBox Addcontent(string context, bool isBold = false)
        {
            if (string.IsNullOrEmpty(context))
                return this;

            this.SelectionFont = isBold ? BoldFont : RegularFont;
            if (this.Text.Length > 0 && this.Text.Last() != ' ')
            {
                this.AppendText(" ");
            }
            this.AppendText(context);
            return this;
        }
    }
}
