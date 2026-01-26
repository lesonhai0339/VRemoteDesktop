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

        private readonly MainPresenter _mainPresenter;
        private readonly ComponentResourceManager resources = new ComponentResourceManager(typeof(FormMain));
        public FormMain(RemoteService remoteControlService)
        {
            InitializeComponent();
            //_remoteDesktopService = remoteDesktopService;
            //ViewModel = new MainViewModel(_remoteDesktopService);
            //SetupBinding();
            RegisterChatForm();
            isShow = false;


            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            _mainPresenter = new MainPresenter(remoteControlService );
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
                default:
                    break;
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
        #region Properties
        //public MainViewModel ViewModel
        //{
        //    get
        //    {
        //        lock (_lock)
        //        {
        //            return _viewModel;
        //        }
        //    }
        //    set
        //    {
        //        lock (_lock)
        //        {
        //            if(_viewModel != null)
        //            {
        //                _viewModel.ClientAcceptRequestRemote -= ClientAcceptRequestRemoteEventHandler;
        //            }
        //            _viewModel = value;
        //            if(_viewModel != null)
        //            {
        //                _viewModel.ClientAcceptRequestRemote += ClientAcceptRequestRemoteEventHandler;
        //            }
        //        }
        //    }
        //}


        #endregion
        private void RegisterChatForm()
        {
            chatForm = new FormChat();
            chatForm.FormClosed += ChatForm_ClosedEventHandler;
        }
        private void SetupBinding()
        {

            //txtOwnerId.DataBindings.Add("Text", ViewModel, "MyId",
            //    false, DataSourceUpdateMode.OnPropertyChanged);

            //txtOwnerPassword.DataBindings.Add("Text", ViewModel, "MyPassword",
            //   false, DataSourceUpdateMode.OnPropertyChanged);

            //_viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)(() => OnViewModelPropertyChanged(sender, e)));
                return;
            }
            //if(e.PropertyName == "ConnectStatus")
            //{
            //    UpdateConnectionStatus(_viewModel.ConnectStatus);
            //}
            //else if(e.PropertyName == "ErrorMessage")
            //{
            //    MsgBox.Show(_viewModel.ErrorMessage, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
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

            //if(_viewModel != null)
            //    _viewModel.ClientAcceptRequestRemote -= ClientAcceptRequestRemoteEventHandler;

            //_viewModel.Dispose();
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
        //private void Connect()
        //{
        //    //ViewModel.Connect();
        //}
        //private void P2PConnect(string id, string password, bool useTurnServer = false)
        //{
        //    //iewModel.RequestP2PConnect(id, password, useTurnServer);
        //}
        //private void UpdateConnectionStatus(ConnectionStatus status)
        //{
        //    Action action = () =>
        //    {
        //        lbStatus.Text = (status == ConnectionStatus.Connected) ? resources.GetString("CODE_SUCCESS_READY") : 
        //                        (status == ConnectionStatus.Disconnected) ? resources.GetString("CODE_ERROR_LOST_CONNECTION") :
        //                        resources.GetString("CODE_ERROR_NOT_READY");

        //        pnStatus.Invalidate();
        //    };


        //    if (this.InvokeRequired)
        //    {
        //        this.Invoke(action);
        //    }
        //    else
        //    {
        //        action();
        //    }
        //}
        //private void ClientAcceptRequestRemoteEventHandler(object sender ,RemoteDesktopEventArgs e)
        //{
        //    if(sender is ClientSession clientSession)
        //    {
        //        if(e.Type == SocketDataType.RemoteControlAcceptedRequestToConnect || (e.Type == SocketDataType.P2PLoginRespond && e.Flag == true))
        //        {
        //            OpenRemoteForm(clientSession);
        //        }
        //        else if (e.Type == SocketDataType.P2PLoginRespond && e.Flag == false)
        //        {
        //            //try use TURN SERVER
        //            string partnerId = txtPartnerId.Text.Replace(" ", "");
        //            string partnerPassword = txtPartnerPassword.Text.Replace(" ", "");

        //            //_viewModel.RequestP2PConnect(partnerId, partnerPassword, true);
        //        }
        //        else if (e.Type == SocketDataType.Ready)
        //        {
        //            AddChat(clientSession);
        //        }
        //        else if (e.Type == SocketDataType.RemoteControlRefusedRequestToConnect)
        //        {
        //            MessageBox.Show("Đối tác từ chối kết nối", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //        else if (e.Type == SocketDataType.RemoteControlConnectFailed)
        //        {
        //            MessageBox.Show("Kết nối đến máy khách thất bại", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //}
        //private void OpenRemoteForm(ClientSession clientSession)
        //{
        //    //if (this.InvokeRequired)
        //    //{
        //    //    this.Invoke(new Action<ClientSession>(OpenRemoteForm), clientSession);
        //    //    return;
        //    //}

        //    //FormRemote remoteForm = new FormRemote(clientSession, _remoteDesktopService);

        //    //remoteForm.Show();
        //    //AddChat(clientSession);
        //}
        //private void AddChat(ClientSession clientSession)
        //{
        //    if (this.InvokeRequired)
        //    {
        //        this.Invoke(new Action<ClientSession>(AddChat), clientSession);
        //        return;
        //    }

        //    if (clientSession == null) return;

        //    if (!isShow)
        //    {
        //        if(chatForm == null)
        //        {
        //            RegisterChatForm();
        //        }
        //        //need to cal event from ChatForm to this to set isShow = false when Chat form disposed
        //        chatForm.Show();
        //        isShow = true;
        //    }
        //    chatForm.AddConnection(clientSession.SessionId, clientSession);
        //}
    }
}
