using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.IO;
using Vsign4.VRemoteDesktop.Presenters;
using Vsign4.VRemoteDesktop.Presenters.Enums;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Presenters.Events;
using Vsign4.VRemoteDesktop.Services.Machine.DTOs;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Presenters.DTOs;
using Vsign4.VRemoteDesktop.Utils;
using LoginResponse = Vsign4.VRemoteDesktop.Presenters.DTOs.LoginResponse;

namespace Vsign4.VRemoteDesktop.Views
{
    public partial class frmRemote : Form
    {
        private readonly object _lock = new object();
        private bool isShow;
        private frmRemoteChat chatForm;
        private LoginStatus _status;

        private readonly RemoteService _remoteService;
        private readonly MainPresenter _mainPresenter;

        private readonly ComponentResourceManager resource = new ComponentResourceManager(typeof(frmRemote));
        public frmRemote(RemoteService remoteService)
        {
            InitializeComponent();
            isShow = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;

            _remoteService = remoteService;
            _mainPresenter = new MainPresenter(_remoteService);
            _mainPresenter.OnData += OnDataEventHandler;
            _mainPresenter.OnError += OnErrorEventHandler;
            _mainPresenter.OnP2PConnectionChanged += OnP2PConnectionChangedEventHandler;    
            RegisterChatForm();
        }

        private void OnP2PConnectionChangedEventHandler(object sender, P2PConnectionChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                if (!this.IsDisposed)
                {
                    this.Invoke(new Action<object, P2PConnectionChangedEventArgs>(OnP2PConnectionChangedEventHandler), sender, e);
                }
                return;
            }
            this.lbP2PConnectStatus.Text = e.Message;   
        }

        private void UpdateIdAndPassword(string id, string password)
        {
            txtOwnerId.Text = _mainPresenter.StringToStringWithDelimiter(id, " ", 3);
            txtOwnerPassword.Text = password;
            this.Invalidate();
        }
        private void RegisterChatForm()
        {
            chatForm = new frmRemoteChat();
            chatForm.FormClosed += ChatForm_ClosedEventHandler;
        }
        private void pnStatus_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color circleColor = (_status == LoginStatus.Connected) ? Color.Green :
                                (_status == LoginStatus.Connecting) ? Color.Orange :
                                (_status == LoginStatus.Disconnected) ? Color.Red
                                : Color.DarkGray;

            using (SolidBrush brush = new SolidBrush(circleColor))
            {
                g.FillEllipse(brush, 1, 1, pnStatus.Width - 2, pnStatus.Height - 2);
            }
        }

        private void frmRemote_Load(object sender, EventArgs e)
        {

        }
        private void frmRemote_Shown(object sender, EventArgs e)
        {
            _mainPresenter.Initialize();
            _mainPresenter.Login();
            var data = _remoteService.LoadConnectHistory();
            if (data != null)
            {
                listViewControl1.LoadHistory(data);
            }
        }
        private void ChatForm_ClosedEventHandler(object sender, FormClosedEventArgs e)
        {
            chatForm.FormClosed -= ChatForm_ClosedEventHandler;
            chatForm = null;
            isShow = false;
        }
        private void frmRemote_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (chatForm != null && !chatForm.IsDisposed)
                chatForm.Close();

            if (_mainPresenter != null)
            {
                _mainPresenter.OnData -= OnDataEventHandler;
                _mainPresenter.OnError -= OnErrorEventHandler;

                _mainPresenter.Dispose();
            }
        }
        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (_status != LoginStatus.Connected)
            {

                MessageBox.Show("Không thể kết nối đến máy chủ", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string partnerId = listViewControl1.InputText.Replace(" ", "");
            string partnerPassword = txtPartnerPassword.Text.Replace(" ", "");

            if (string.IsNullOrWhiteSpace(partnerId))
            {
                MessageBox.Show("Id đối tác không được bỏ trống", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(partnerPassword))
            {
                MessageBox.Show("Mật khẩu đối tác không được bỏ trống", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_mainPresenter.CheckIdConnected(partnerId))
            {
                MessageBox.Show("Đã kết nối với đối tác", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            _mainPresenter.GetPartnerInfo(partnerId, partnerPassword);
            _mainPresenter.StartP2PConnectStatus();
        }
        private void OpenRemoteForm(ClientSession clientSession)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<ClientSession>(OpenRemoteForm), clientSession);
                return;
            }
            frmRemoteControl remoteForm = new frmRemoteControl(clientSession, _remoteService);

            remoteForm.Show();
            AddChat(clientSession);
        }
        private void AddChat(ClientSession clientSession)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<ClientSession>(AddChat), clientSession);
                return;
            }

            if (clientSession == null) return;

            if (!isShow)
            {
                if (chatForm == null)
                {
                    RegisterChatForm();
                }
                //need to cal event from ChatForm to this to set isShow = false when Chat form disposed
                chatForm.Show();
                isShow = true;
            }
            chatForm.AddChatSession(clientSession.SessionId, clientSession);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
        public Bitmap ReSizeImage(Image image, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                g.DrawImage(image, 0, 0, width, height);
            }
            return bmp;
        }

        private void btnSetDefaultPassword_Click(object sender, EventArgs e)
        {
            frmSetDefaultPassword frmSetDefaultPassword = new frmSetDefaultPassword();

            if (frmSetDefaultPassword.ShowDialog() == DialogResult.OK)
            {
                string password= frmSetDefaultPassword.defaultPassword;
                if (!_mainPresenter.SetDefaultPassword(password))
                {
                    MessageBox.Show("Xảy ra lỗi trong quá trình cập nhật mật khẩu mặc định", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("Cập nhật thành công, mật khẩu mới sẽ có hiệu lực trong lần khởi động tiếp theo", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
        #region Events
        private void OnErrorEventHandler(object sender, MainErrorEventArgs e)
        {
            MessageBox.Show(
                this,
                e.Ex.Message,
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }

        private void OnDataEventHandler(object sender, MainDataEventArgs e)
        {
            DataDisPatcher(e.Data);
        }
        private void DataDisPatcher(object data)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<object>(DataDisPatcher), data);
                return;
            }

            var machine = data as MachineInfo;
            if (machine != null)
            {
                UpdateIdAndPassword(machine.Id, machine.Password);
            }
            else
            {
                var login = data as LoginResponse;
                if (login != null)
                {
                    UpdateConnectStatus(login);
                }
                else
                {
                    var partner = data as PartnerInfoResponse;
                    if (partner != null)
                    {
                        GetPartnerInfoResponse(partner.Message);
                    }
                    else
                    {
                        var newCon = data as NewRemoteConnection;
                        if (newCon != null)
                        {
                            AddNewRemoteConnection(newCon);
                        }
                    }
                }
            }

        }
        private void AddNewRemoteConnection(NewRemoteConnection newCon)
        {
            try
            {
                //Add to RemoteFrm
                if (newCon.IsController)
                {
                    _mainPresenter.SaveConnectInfo(newCon.ClientSession);
                    OpenRemoteForm(newCon.ClientSession);
                }

                //Add to ChatFrm
                AddChat(newCon.ClientSession);

            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "frmMain").Error(ex, "AddNewRemoteConnection err ");
                MessageBox.Show("Thiết lập kết nối thất bại", "Xảy ra lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void GetPartnerInfoResponse(string message)
        {
            MessageBox.Show(message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        private void UpdateConnectStatus(Presenters.DTOs.LoginResponse response)
        {
            _status = response.Type;
            lbStatus.Text = response.Message;
            pnStatus.Invalidate();
        }

        #endregion
    }
}
