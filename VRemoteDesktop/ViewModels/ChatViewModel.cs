using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Layouts;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;

namespace VRemoteDesktop.ViewModels
{
    public class ChatViewModel: INotifyPropertyChanged, IDisposable
    {
        private readonly Dictionary<string, VClient> _clients;
        private string _clientAdded;
        private string _clientRemoved;
        public ChatViewModel()
        {
            _clients = new Dictionary<string, VClient>();
        }
        public string ClientAdded
        {
            get => _clientAdded;
            private set
            {
                _clientAdded = value;
                OnPropertyChanged("ClientAdded");
            }
        }
        public string ClientRemoved
        {
            get => _clientRemoved;
            private set
            {
                _clientAdded = value;
                OnPropertyChanged("ClientRemoved");
            }
        }
        public void UpdateConnection(string key, VClient value)
        {
            _clients.Add(key, value);
            ClientAdded = key;
        }
        public void RemoveConnection(string key)
        {
            _clients.Remove(key);
            ClientRemoved = key;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public TableLayoutPanel NewControl(string id)
        {
            if(_clients.TryGetValue(id, out var client))
            {
                CustomTableLayout table = new CustomTableLayout(client.MyId, null)
                               .SetColAndRow(2, 1)
                               .SetStyles(
                                   new UIPropertyRegistration(nameof(BorderStyle), BorderStyle.FixedSingle)
                               )
                               .SetColumAndRowStyle(
                                   new List<ColumnStyle>
                                   {
                        new ColumnStyle(SizeType.Percent, 20F),
                        new ColumnStyle(SizeType.Percent, 80F)
                                   },
                                   new List<RowStyle>
                                   {
                        new RowStyle(SizeType.AutoSize),
                                   }
                               );

                Label lbChat = new Label
                {
                    Text = client.Partner.ComputerName,
                    Name = client.SocketId,
                    AutoSize = true
                };
                CustomPanel pnStatus = new CustomPanel();
                pnStatus.Paint += pnStatus.CreateCircle;

                table.AddControl(nameof(pnStatus), pnStatus, 0, 0);
                table.AddControl(nameof(lbChat), lbChat, 1, 0);

                return table.Table;
            }
            return null;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
        }
 
    }
}
