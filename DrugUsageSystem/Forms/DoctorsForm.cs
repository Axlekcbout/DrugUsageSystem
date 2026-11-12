using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>
    /// Список всех аккаунтов (логинов) врачей. Доступен только главврачу (администратору).
    /// Главврач может назначать и снимать роль главврача у других врачей.
    /// </summary>
    public class DoctorsForm : Form
    {
        private readonly DataStore _store;
        private readonly Doctor _currentDoctor;
        private readonly DataGridView _grid;
        private readonly Label _hint;

        public DoctorsForm(DataStore store, Doctor currentDoctor)
        {
            _store = store;
            _currentDoctor = currentDoctor;

            Text = "Пользователи — список логинов";
            ClientSize = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(600, 400);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            // Защита на случай прямого вызова: без прав главврача список не показываем
            if (!currentDoctor.IsAdmin)
            {
                var deny = new Label
                {
                    Text = "Список аккаунтов доступен только главврачу.",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = Ui.BoldFont,
                    ForeColor = Ui.Danger
                };
                Controls.Add(deny);
                Shown += (s, e) => Close();
                return;
            }

            var title = new Label
            {
                Text = "Зарегистрированные аккаунты",
                Font = Ui.TitleFont,
                ForeColor = Ui.SidebarBg,
                Dock = DockStyle.Top,
                Height = 44,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            };

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 92,
                Padding = new Padding(12, 6, 12, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = Ui.ContentBg
            };
            var btnAdd = Ui.ActionButton("Добавить пользователя");
            var btnRename = Ui.ActionButton("Изменить логин", false);
            var btnRole = Ui.ActionButton("Изменить должность", false);
            var btnPromote = Ui.ActionButton("Назначить главврачом", false);
            var btnDemote = Ui.ActionButton("Снять главврача", false);
            var btnDelete = Ui.ActionButton("Удалить аккаунт", false);
            btnAdd.Click += OnAddDoctor;
            btnRename.Click += OnChangeLogin;
            btnRole.Click += OnChangeRole;
            btnPromote.Click += (s, e) => SetAdmin(true);
            btnDemote.Click += (s, e) => SetAdmin(false);
            btnDelete.Click += OnDeleteAccount;
            btnDelete.ForeColor = Ui.Danger;
            top.Controls.Add(btnAdd);
            top.Controls.Add(btnRename);
            top.Controls.Add(btnRole);
            top.Controls.Add(btnPromote);
            top.Controls.Add(btnDemote);
            top.Controls.Add(btnDelete);

            _grid = Ui.Grid();
            _grid.Columns.Add("colLogin", "Логин");
            _grid.Columns.Add("colName", "Имя");
            _grid.Columns.Add("colRole", "Роль");
            _grid.Columns.Add("colCreated", "Дата регистрации");
            _grid.Columns.Add("colCards", "Карточек");
            _grid.Columns[0].FillWeight = 22;
            _grid.Columns[1].FillWeight = 28;
            _grid.Columns[2].FillWeight = 16;
            _grid.Columns[3].FillWeight = 20;
            _grid.Columns[4].FillWeight = 14;

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 4), BackColor = Ui.ContentBg };
            body.Controls.Add(_grid);

            _hint = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(title);
            Controls.Add(_hint);

            RefreshList();
        }

        private void RefreshList()
        {
            _grid.Rows.Clear();
            var cardsCount = new PatientRepository(_store).CardsPerDoctor();
            foreach (var d in _store.Data.Doctors.OrderBy(x => x.Login))
            {
                int idx = _grid.Rows.Add(d.Login, d.DisplayName,
                    d.DisplayRole + (d.IsAdmin ? " ★" : ""),
                    d.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                    cardsCount.TryGetValue(d.Id, out int n) ? n : 0);
                if (d.Id == _currentDoctor.Id)
                    _grid.Rows[idx].DefaultCellStyle.Font = Ui.BoldFont;
            }

            int shared = cardsCount.TryGetValue(0, out int s) ? s : 0;
            _hint.Text = "Жирным выделен ваш логин: " + _currentDoctor.Login +
                         ".   Общих карточек (видны всем): " + shared + ".";
        }

        private Doctor SelectedDoctor()
        {
            if (_grid.SelectedRows.Count == 0)
                return null;
            string login = Convert.ToString(_grid.SelectedRows[0].Cells["colLogin"].Value);
            return _store.Data.Doctors.FirstOrDefault(d => d.Login == login);
        }

        private void SetAdmin(bool makeAdmin)
        {
            var doctor = SelectedDoctor();
            if (doctor == null)
            {
                MessageBox.Show(this, "Сначала выберите врача в таблице.", "Роли",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (doctor.Id == _currentDoctor.Id && !makeAdmin)
            {
                MessageBox.Show(this,
                    "Нельзя снять роль главврача с самого себя —\nиначе можно остаться без администратора.",
                    "Роли", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (doctor.IsAdmin == makeAdmin)
                return; // уже в нужной роли

            doctor.IsAdmin = makeAdmin;
            _store.Save();
            RefreshList();
        }

        // ---------- управление пользователями (только главврач) ----------

        /// <summary>Стандартные должности для выбора (можно ввести и свою).</summary>
        internal static readonly string[] StandardRoles =
            { "Врач", "Медсестра", "Медбрат", "Старшая медсестра", "Фельдшер", "Лаборант" };

        private void OnAddDoctor(object sender, EventArgs e)
        {
            if (!InputDialog.Ask(this, "Новый аккаунт пользователя",
                    new[] { "Логин (минимум 3 символа, без пробелов)", "Имя (как подписывать карточки)", "Пароль (минимум 4 символа)", "Должность (выберите или введите свою)" },
                    new[] { false, false, true, false },
                    3, StandardRoles, out string[] values))
                return;

            var auth = new AuthService(_store);
            if (!auth.Register(values[0], values[1], values[2], values[3], out string error, out var doctor))
            {
                MessageBox.Show(this, error, "Новый аккаунт", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshList();
            MessageBox.Show(this,
                "Аккаунт создан.\nЛогин: " + doctor.Login + "\nДолжность: " + doctor.DisplayRole +
                "\nПередайте логин и пароль сотруднику лично.",
                "Новый аккаунт", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnChangeRole(object sender, EventArgs e)
        {
            var doctor = SelectedDoctor();
            if (doctor == null)
            {
                MessageBox.Show(this, "Сначала выберите пользователя в таблице.", "Смена должности",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (doctor.IsAdmin)
            {
                MessageBox.Show(this,
                    "У главврача должность всегда «Главврач».\nСначала снимите с него роль главврача.",
                    "Смена должности", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!InputDialog.Ask(this, "Изменить должность",
                    new[] { "Новая должность для «" + doctor.Login + "» (сейчас: " + doctor.DisplayRole + ")" },
                    new[] { false },
                    0, StandardRoles, out string[] values))
                return;

            var auth = new AuthService(_store);
            if (!auth.ChangeRole(doctor.Id, values[0], out string error))
            {
                MessageBox.Show(this, error, "Смена должности", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshList();
            MessageBox.Show(this, "Должность изменена на «" + values[0].Trim() + "».",
                "Смена должности", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnDeleteAccount(object sender, EventArgs e)
        {
            var doctor = SelectedDoctor();
            if (doctor == null)
            {
                MessageBox.Show(this, "Сначала выберите пользователя в таблице.", "Удаление аккаунта",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var cardsCount = new PatientRepository(_store).CardsPerDoctor();
            int cards = cardsCount.TryGetValue(doctor.Id, out int n) ? n : 0;
            string cardNote = cards > 0
                ? "\n\nУ этого пользователя " + cards + " карточек пациентов —\nони станут общими (видны всем врачам)."
                : "";

            if (MessageBox.Show(this,
                    "Удалить аккаунт «" + doctor.Login + "» (" + doctor.DisplayRole + ")?" +
                    cardNote + "\n\nВосстановить аккаунт будет нельзя.",
                    "Удаление аккаунта", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            var auth = new AuthService(_store);
            if (!auth.DeleteAccount(doctor.Id, _currentDoctor.Id, out string error))
            {
                MessageBox.Show(this, error, "Удаление аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshList();
            MessageBox.Show(this, "Аккаунт «" + doctor.Login + "» удалён.",
                "Удаление аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnChangeLogin(object sender, EventArgs e)
        {
            var doctor = SelectedDoctor();
            if (doctor == null)
            {
                MessageBox.Show(this, "Сначала выберите врача в таблице.", "Смена логина",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!InputDialog.Ask(this, "Изменить логин",
                    new[] { "Новый логин для «" + doctor.Login + "»" },
                    new[] { false }, out string[] values))
                return;

            var auth = new AuthService(_store);
            if (!auth.ChangeLogin(doctor.Id, values[0], out string error))
            {
                MessageBox.Show(this, error, "Смена логина", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RefreshList();
            MessageBox.Show(this, "Логин изменён на «" + values[0].Trim().ToLowerInvariant() + "».",
                "Смена логина", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>Простое модальное окно с полями ввода (без designer-файлов).</summary>
    internal static class InputDialog
    {
        public static bool Ask(IWin32Window owner, string title, string[] labels, bool[] isPassword, out string[] values)
        {
            return Ask(owner, title, labels, isPassword, -1, null, out values);
        }

        /// <summary>
        /// Вариант с выпадающим списком: поле с индексом comboIndex рисуется как ComboBox
        /// (редактируемый — можно выбрать из списка или ввести своё значение).
        /// </summary>
        public static bool Ask(IWin32Window owner, string title, string[] labels, bool[] isPassword,
            int comboIndex, string[] comboItems, out string[] values)
        {
            values = null;
            using (var f = new Form())
            {
                f.Text = title;
                f.ClientSize = new Size(380, 50 + labels.Length * 56 + 48);
                f.StartPosition = FormStartPosition.CenterParent;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false;
                f.MinimizeBox = false;
                f.Font = Ui.BaseFont;
                f.BackColor = Color.White;

                var boxes = new Control[labels.Length];
                int y = 12;
                for (int i = 0; i < labels.Length; i++)
                {
                    f.Controls.Add(new Label
                    {
                        Text = labels[i],
                        Left = 16,
                        Top = y,
                        Width = 348,
                        Height = 20
                    });

                    Control box;
                    if (i == comboIndex && comboItems != null && comboItems.Length > 0)
                    {
                        var combo = new ComboBox
                        {
                            Left = 16,
                            Top = y + 22,
                            Width = 348,
                            DropDownStyle = ComboBoxStyle.DropDown
                        };
                        combo.Items.AddRange(comboItems);
                        combo.SelectedIndex = 0;
                        box = combo;
                    }
                    else
                    {
                        box = new TextBox
                        {
                            Left = 16,
                            Top = y + 22,
                            Width = 348,
                            UseSystemPasswordChar = isPassword != null && i < isPassword.Length && isPassword[i]
                        };
                    }
                    boxes[i] = box;
                    f.Controls.Add(box);
                    y += 56;
                }

                var ok = Ui.ActionButton("OK");
                ok.Left = 136;
                ok.Top = y + 6;
                ok.DialogResult = DialogResult.OK;
                var cancel = Ui.ActionButton("Отмена", false);
                cancel.Left = 252;
                cancel.Top = y + 6;
                cancel.DialogResult = DialogResult.Cancel;
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;

                if (f.ShowDialog(owner) != DialogResult.OK)
                    return false;

                values = new string[boxes.Length];
                for (int i = 0; i < boxes.Length; i++)
                    values[i] = boxes[i] is ComboBox combo ? combo.Text : boxes[i].Text;
                return true;
            }
        }
    }
}
