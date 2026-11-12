using System;
using System.Drawing;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Окно входа и регистрации врача (по логину).</summary>
    public class LoginForm : Form
    {
        private readonly AuthService _auth;
        private readonly TextBox _login;
        private readonly TextBox _password;
        private readonly TextBox _displayName;
        private readonly Label _nameLabel;
        private readonly Label _errorLabel;
        private readonly Button _submit;
        private readonly LinkLabel _toggle;
        private bool _registerMode;

        public Doctor LoggedDoctor { get; private set; }

        public LoginForm(DataStore store)
        {
            _auth = new AuthService(store);

            Text = "Система учёта препаратов — вход";
            ClientSize = new Size(400, 380);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            BackColor = Color.White;
            Font = Ui.BaseFont;

            var title = new Label
            {
                Text = "Система учёта\nиспользования препаратов",
                Font = Ui.TitleFont,
                ForeColor = Ui.SidebarBg,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 74
            };

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(36, 6, 36, 10) };

            var loginLabel = new Label { Text = "Логин", Dock = DockStyle.Top, Height = 22 };
            _login = new TextBox { Dock = DockStyle.Top, Height = 28 };
            var gap1 = new Panel { Dock = DockStyle.Top, Height = 10 };

            var passLabel = new Label { Text = "Пароль", Dock = DockStyle.Top, Height = 22 };
            _password = new TextBox { Dock = DockStyle.Top, Height = 28, UseSystemPasswordChar = true };
            var gap2 = new Panel { Dock = DockStyle.Top, Height = 10 };

            _nameLabel = new Label { Text = "Имя (как подписывать карточки)", Dock = DockStyle.Top, Height = 22, Visible = false };
            _displayName = new TextBox { Dock = DockStyle.Top, Height = 28, Visible = false };
            var gap3 = new Panel { Dock = DockStyle.Top, Height = 10 };

            _errorLabel = new Label
            {
                Text = "",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Ui.Danger,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _submit = Ui.ActionButton("Войти");
            _submit.Dock = DockStyle.Top;
            _submit.Click += OnSubmit;

            var gap4 = new Panel { Dock = DockStyle.Top, Height = 8 };

            _toggle = new LinkLabel
            {
                Text = "Нет аккаунта? Зарегистрироваться",
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Ui.Accent
            };
            _toggle.Click += (s, e) => ToggleMode();

            // Dock=Top: чем позже добавлен, тем выше в колонке.
            panel.Controls.Add(_toggle);
            panel.Controls.Add(gap4);
            panel.Controls.Add(_submit);
            panel.Controls.Add(_errorLabel);
            panel.Controls.Add(gap3);
            panel.Controls.Add(_displayName);
            panel.Controls.Add(_nameLabel);
            panel.Controls.Add(gap2);
            panel.Controls.Add(_password);
            panel.Controls.Add(passLabel);
            panel.Controls.Add(gap1);
            panel.Controls.Add(_login);
            panel.Controls.Add(loginLabel);

            Controls.Add(panel);
            Controls.Add(title);

            AcceptButton = _submit;
        }

        private void ToggleMode()
        {
            _registerMode = !_registerMode;
            _nameLabel.Visible = _displayName.Visible = _registerMode;
            _submit.Text = _registerMode ? "Создать аккаунт" : "Войти";
            _toggle.Text = _registerMode ? "Уже есть аккаунт? Войти" : "Нет аккаунта? Зарегистрироваться";
            _errorLabel.Text = "";
        }

        private void OnSubmit(object sender, EventArgs e)
        {
            bool ok;
            string error;
            Doctor doctor;

            if (_registerMode)
                ok = _auth.Register(_login.Text, _displayName.Text, _password.Text, out error, out doctor);
            else
                ok = _auth.Login(_login.Text, _password.Text, out error, out doctor);

            if (!ok)
            {
                _errorLabel.Text = error;
                return;
            }

            LoggedDoctor = doctor;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
