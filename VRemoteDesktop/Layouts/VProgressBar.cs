using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class VProgressBar: ProgressBar
    {
        private long _totalSize;
        public VProgressBar(FileReceivedInfo fileInfo)
        {
            if (fileInfo == null)
                throw new ArgumentNullException(nameof(fileInfo));

            if (string.IsNullOrWhiteSpace(fileInfo.Filename))
                throw new ArgumentException("Filename cannot be null or empty", nameof(fileInfo));

            if (fileInfo.FileSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(fileInfo.FileSize), "FileSize must be greater than zero");

            InitializeComponent(fileInfo);
        }
        private void InitializeComponent(FileReceivedInfo fileInfo)
        {
            this.Visible = true;
            this.Minimum = 0;
            this.Maximum = 100;
            this.Value = 0;

            _totalSize = fileInfo.FileSize;
        }
        public void SetStep(int num)
        {
            var value = (int)(num * 100.0 / _totalSize);
            this.Value = Math.Min(this.Maximum, (int)value);
        }
    }
}
