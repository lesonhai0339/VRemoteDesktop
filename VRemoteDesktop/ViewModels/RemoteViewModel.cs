using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using VRemoteServer.Models;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.ViewModels
{
    public class RemoteViewModel : INotifyPropertyChanged
    {
        private ClientInfo _client;
        public Action<byte[]> ScreenEvent;
        public Action<byte[]> ScreenChunksEvent;
        public RemoteViewModel(ClientInfo client)
        {
            Client = client;
        }
        #region Properties
        public ClientInfo Client
        {
            get => _client;
            private set
            {
                _client = value;
            }
        }
        #endregion
        #region Methods
        public void DataReceived(ScreenType type, byte[] data)
        {
            if(type == ScreenType.FULLSCREEN)
            {
                ScreenEvent?.Invoke(data);
            }
            if(type == ScreenType.REGIONSCREENS)
            {
                ScreenChunksEvent?.Invoke(data);
            }
        }
        #endregion
        #region EventHandlers
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
