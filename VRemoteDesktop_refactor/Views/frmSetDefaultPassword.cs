using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Helpers;

namespace Vsign4.VRemoteDesktop.Views
{
    public partial class frmSetDefaultPassword : Form
    {
        public string defaultPassword;
        public frmSetDefaultPassword()
        {
            InitializeComponent();
            this.StartPosition =  FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }

        private void frmSetDefaultPassword_Load(object sender, EventArgs e)
        {

        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            string password = txtPassword.Text;
            string verifyPassword = txtVerifyPassword.Text;
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Mật khẩu không được bỏ trống", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(verifyPassword))
            {
                MessageBox.Show("Mật khẩu xác thực không được bỏ trống", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.Equals(password, verifyPassword))
            {
                MessageBox.Show("Mật khẩu và mật khẩu xác thực không trùng khớp", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (password.Length < 6)
            {
                MessageBox.Show("Mật khẩu ít nhất phải có 6 ký tự", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (password.Length > 256)
            {
                MessageBox.Show("Mật khẩu tối đa chứa 256 ký tự", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!Validator.ContainSpecialCharacterRegex(password))
            {
                MessageBox.Show("Mật khẩu phải có ít nhất một ký tự đăc biệt", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            defaultPassword = password;
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.Close();
        }
        
    }
}
