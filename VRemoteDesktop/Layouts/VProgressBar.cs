using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Layouts
{
    public class VProgressBar : ProgressBar
    {
        private long _totalSize;
        private float _received;
        public event EventHandler<EventArgs> ProgressCompleted;
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
            this.Step = 1;

            _totalSize = fileInfo.FileSize;
            _received = 0;
        }
        public void SetStep(int length)
        {
            _received += length;
            if (_received < (_totalSize / 100))
                return;

            int step = (int)(_received / (_totalSize * 1.0 / 100));
            float remain = _received % (long)(_totalSize * 1.0 / 100);
            _received = remain;

            for (int i = 0; i < step && this.Value < this.Maximum; i++)
            {
                this.PerformStep();
            }

            if (this.Value == this.Maximum)
                ProgressCompleted?.Invoke(this, new EventArgs());
        }
    }
}
