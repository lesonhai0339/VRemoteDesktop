using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.ViewModels;

namespace VRemoteDesktop.Views
{
    public partial class FormChat : Form
    {
        private ChatViewModel _chatViewModel;
        public FormChat()
        {
            InitializeComponent();
            ChatView = new ChatViewModel();
            SetupBinding();



            fpnNumberChatConnection.BorderStyle = BorderStyle.FixedSingle;
            fpnNumberChatConnection.FlowDirection = FlowDirection.TopDown;
            fpnNumberChatConnection.WrapContents = false;
        }
        public ChatViewModel ChatView
        {
            get => _chatViewModel;
            private set => _chatViewModel = value;
        }
        private void SetupBinding()
        {
            _chatViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(_chatViewModel.ClientAdded))
            {
                var value = _chatViewModel.ClientAdded;

                var control = _chatViewModel.NewControl(value);

                fpnNumberChatConnection.Controls.Add(control);
            }
            if (e.PropertyName == nameof(_chatViewModel.ClientRemoved))
            {
                var value = _chatViewModel.ClientRemoved;

                var lbs = fpnNumberChatConnection.Controls.Find(value, true);

                foreach(var lb in lbs)
                {
                    fpnNumberChatConnection.Controls.Remove(lb);
                }
            }
        }
        private void FormChat_Load(object sender, EventArgs e)
        {
        }

        private void fpnNumberChatConnection_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
