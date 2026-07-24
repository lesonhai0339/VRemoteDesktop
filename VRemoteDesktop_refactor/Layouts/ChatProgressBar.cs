using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.FileService.DTOs;

namespace Vsign4.VRemoteDesktop.Layouts
{
    public class ChatProgressBar : ProgressBar
    {
        private const int TIMEOUT = 60; //minutes unit
        private long _totalSize;
        private float _received;
        private System.Threading.Timer _timeout;
        private DateTime _time;

        public event EventHandler<ChatProgressBarEventArgs> ProgressBarEvent;
        public ChatProgressBar(VFileInfo fileInfo)
        {
            if (fileInfo == null)
                throw new ArgumentNullException("fileInfo");

            if (string.IsNullOrWhiteSpace(fileInfo.Filename))
                throw new ArgumentException("Filename cannot be null or empty");

            if (fileInfo.FileSize <= 0)
                throw new ArgumentOutOfRangeException("FileSize must be greater than zero");

            InitializeComponent(fileInfo);
            _timeout = new System.Threading.Timer(CheckTimeOut, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }
        private void InitializeComponent(VFileInfo fileInfo)
        {
            this.Visible = true;
            this.Minimum = 0;
            this.Maximum = 100;
            this.Value = 0;
            this.Step = 1;

            _totalSize = fileInfo.FileSize;
            _received = 0;
            _time = DateTime.Now;
        }
        private void CheckTimeOut(object obj)
        {
            DateTime curTime = DateTime.Now;
            var elapsed = (curTime - _time).TotalMinutes;
            if (elapsed > TIMEOUT)
            {
                if(_timeout != null)
                    _timeout.Dispose();

                if(ProgressBarEvent != null)
                    ProgressBarEvent.Invoke(this, new ChatProgressBarEventArgs(ProgressBarEnum.Timeout));
            }
        }
        public void SetStep(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException("Step length must be greater than zero");

            _time = DateTime.Now;
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
            {
                if(ProgressBarEvent != null)
                    ProgressBarEvent.Invoke(this, new ChatProgressBarEventArgs(ProgressBarEnum.Finished));
            }
        }
    }
}
