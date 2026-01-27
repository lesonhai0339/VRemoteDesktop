using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Presenters;
using VRemoteDesktop.Presenters.DTOs;
using VRemoteDesktop.Presenters.Enums;
using VRemoteDesktop.Presenters.Events;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Machine.DTOs;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using VRemoteDesktop.ViewModels;
using VRemoteDesktop.Views;
using VRemoteServer.Models;

namespace VRemoteDesktop
{
    public partial class FormMain : Form
    {
        private readonly object _lock = new object();
        //private MainViewModel _viewModel;
        private FormChat chatForm;
        private bool isShow;
        private LoginStatus _status;

        private readonly RemoteService _remoteService;
        private readonly MainPresenter _mainPresenter;
        private readonly ComponentResourceManager resources = new ComponentResourceManager(typeof(FormMain));
        public FormMain(RemoteService remoteService)
        {
            InitializeComponent();
            _remoteService = remoteService;
            //_remoteDesktopService = remoteDesktopService;
            //ViewModel = new MainViewModel(_remoteDesktopService);
            //SetupBinding();
            RegisterChatForm();
            isShow = false;


            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            _mainPresenter = new MainPresenter(_remoteService);
            _mainPresenter.OnData += OnDataEventHandler;
            _mainPresenter.OnError += OnErrorEventHandler;

        }

        private void OnErrorEventHandler(object sender, MainErrorEventArgs e)
        {
            MessageBox.Show(e.Ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            switch (data)
            {
                case MachineInfo machine:
                    UpdateIdAndPassword(machine.Id, machine.Password);
                    break;
                case LoginResponse response:
                    UpdateConnectStatus(response);
                    break;
                case PartnerInfoResponse response:
                    GetPartnerInfoResponse(response.Message);
                    break;
                case NewRemoteConnection newCon:
                    AddNewRemoteConnection(newCon);
                    break;
                default:
                    break;
            }
        }

        private void AddNewRemoteConnection(NewRemoteConnection newCon)
        {
            try
            {
                //Add to RemoteFrm
                if(newCon.IsController)
                    OpenRemoteForm(newCon.ClientSession);

                //Add to ChatFrm
                AddChat(newCon.ClientSession);

            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("FileName", "frmMain").Error(ex, "AddNewRemoteConnection err ");
                MessageBox.Show("Thiết lập kết nối thất bại", "Xảy ra lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GetPartnerInfoResponse(string message)
        {
            MessageBox.Show(message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void UpdateConnectStatus(LoginResponse response)
        {
            _status = response.Type;
            lbStatus.Text = response.Message;
            pnStatus.Invalidate();
        }

        private void UpdateIdAndPassword(string id, string password)
        {
            txtOwnerId.Text = _mainPresenter.StringToStringWithDelimiter(id, " ", 3);
            txtOwnerPassword.Text = password;
            this.Invalidate();
        }
        private void RegisterChatForm()
        {
            chatForm = new FormChat();
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

        private void FormMain_Load(object sender, EventArgs e)
        {

        }
        private void FormMain_Shown(object sender, EventArgs e)
        {
           _mainPresenter.Initialize();
            _mainPresenter.Login();
        }
        private void ChatForm_ClosedEventHandler(object sender, FormClosedEventArgs e)
        {
            chatForm.FormClosed -= ChatForm_ClosedEventHandler;
            chatForm = null;
            isShow = false;
        }
        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
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
            string partnerId = txtPartnerId.Text.Replace(" ", "");
            string partnerPassword = txtPartnerPassword.Text.Replace(" ","");

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
        }
        private void OpenRemoteForm(ClientSession clientSession)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action<ClientSession>(OpenRemoteForm), clientSession);
                return;
            }

            FormRemote remoteForm = new FormRemote(clientSession, _remoteService);

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
            chatForm.AddConnection(clientSession.SessionId, clientSession);
        }
    }
}
