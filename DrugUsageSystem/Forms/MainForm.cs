using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Главное окно: сайдбар слева, дашборд со статистикой справа.</summary>
    public class MainForm : Form
    {
        private readonly DataStore _store;
        private readonly Doctor _doctor;
        private readonly string _appDir;

        private Label _statTotal;
        private Label _statMine;
        private Label _statToday;
        private Label _statDrugs;
        private DataGridView _recentGrid;

        public MainForm(DataStore store, Doctor doctor)
        {
            _store = store;
            _doctor = doctor;
            _appDir = System.IO.Path.GetDirectoryName(store.FilePath);

            Text = "Система учёта использования препаратов — " + doctor.Login;
            ClientSize = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(980, 620);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            BuildSidebar();
            BuildDashboard();
            RefreshDashboard();
        }

        // ---------------- сайдбар ----------------

        private void BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Ui.SidebarBg };

            var header = new Panel { Dock = DockStyle.Top, Height = 86, BackColor = Ui.SidebarBg, Padding = new Padding(18, 14, 10, 6) };
            var nameLbl = new Label
            {
                Text = _doctor.DisplayName + (_doctor.IsAdmin ? "  ★" : ""),
                Dock = DockStyle.Top,
                Height = 28,
                Font = Ui.BoldFont,
                ForeColor = Color.White
            };
            var mailLbl = new Label
            {
                Text = "Логин: " + _doctor.Login + "  (" + _doctor.DisplayRole + ")",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(180, 190, 205)
            };
            header.Controls.Add(mailLbl);
            header.Controls.Add(nameLbl);

            var btnPatients = Ui.SidebarButton("Пациенты");
            var btnDirs = Ui.SidebarButton("Справочники");
            var btnReports = Ui.SidebarButton("Отчёты");
            var btnDoctors = Ui.SidebarButton("Пользователи");
            var btnBackup = Ui.SidebarButton("Резервные копии");

            var btnDemo = Ui.SidebarButton("Загрузить демо-данные");
            btnDemo.Dock = DockStyle.Bottom;
            var btnLogout = Ui.SidebarButton("Выйти из аккаунта");
            btnLogout.Dock = DockStyle.Bottom;

            btnPatients.Click += (s, e) => { using (var f = new PatientListForm(_store, _doctor)) f.ShowDialog(this); RefreshDashboard(); };
            btnDirs.Click += (s, e) => { using (var f = new DirectoriesForm(_store)) f.ShowDialog(this); RefreshDashboard(); };
            btnReports.Click += (s, e) => { using (var f = new ReportsForm(_store, _doctor)) f.ShowDialog(this); };
            btnDoctors.Click += (s, e) => { using (var f = new DoctorsForm(_store, _doctor)) f.ShowDialog(this); };
            btnBackup.Click += (s, e) => { using (var f = new BackupForm(_store, _appDir)) f.ShowDialog(this); RefreshDashboard(); };
            btnDemo.Click += OnLoadDemo;
            btnLogout.Click += (s, e) => Close(); // цикл входа в Program.cs снова покажет окно входа

            // При Dock=Top первым добавляется самый верхний элемент.
            sidebar.Controls.Add(header);
            sidebar.Controls.Add(btnPatients);
            sidebar.Controls.Add(btnDirs);
            sidebar.Controls.Add(btnReports);
            // Список аккаунтов виден только главврачу (администратору)
            if (_doctor.IsAdmin)
                sidebar.Controls.Add(btnDoctors);
            sidebar.Controls.Add(btnBackup);
            sidebar.Controls.Add(btnLogout); // прижата к низу
            sidebar.Controls.Add(btnDemo);   // над кнопкой выхода

            Controls.Add(sidebar);
        }

        // ---------------- дашборд ----------------

        private void BuildDashboard()
        {
            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), BackColor = Ui.ContentBg };

            var title = new Label
            {
                Text = "Дашборд",
                Font = Ui.TitleFont,
                ForeColor = Ui.SidebarBg,
                Dock = DockStyle.Top,
                Height = 40
            };
            var subtitle = new Label
            {
                Text = "Здравствуйте, " + _doctor.DisplayName + "! Краткая статистика по базе:",
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = Color.FromArgb(110, 110, 110)
            };

            var cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 116,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Ui.ContentBg
            };
            _statTotal = Ui.StatCard(cards, "Всего карточек", Ui.Accent);
            _statMine = Ui.StatCard(cards, "Мои карточки", Color.FromArgb(84, 162, 75));
            _statToday = Ui.StatCard(cards, "Использований сегодня", Color.FromArgb(228, 87, 86));
            _statDrugs = Ui.StatCard(cards, "Препаратов в справочнике", Color.FromArgb(178, 121, 162));

            var recentTitle = new Label
            {
                Text = "Последние карточки",
                Font = Ui.BoldFont,
                Dock = DockStyle.Top,
                Height = 30
            };

            _recentGrid = Ui.Grid();
            _recentGrid.Columns.Add("colName", "ФИО");
            _recentGrid.Columns.Add("colDept", "Отделение");
            _recentGrid.Columns.Add("colDate", "Дата использования");
            _recentGrid.Columns.Add("colDrugs", "Препараты");
            _recentGrid.Columns.Add("colOwner", "Зарегистрировал");
            _recentGrid.Columns[0].FillWeight = 28;
            _recentGrid.Columns[1].FillWeight = 16;
            _recentGrid.Columns[2].FillWeight = 15;
            _recentGrid.Columns[3].FillWeight = 28;
            _recentGrid.Columns[4].FillWeight = 13;

            content.Controls.Add(_recentGrid);
            content.Controls.Add(recentTitle);
            content.Controls.Add(cards);
            content.Controls.Add(subtitle);
            content.Controls.Add(title);

            Controls.Add(content);
            content.BringToFront(); // контент поверх сайдбара по z-order, докировка сама разведёт
        }

        public void RefreshDashboard()
        {
            var repo = new PatientRepository(_store);
            var all = repo.ForDoctor(_doctor.Id);
            _statTotal.Text = all.Count.ToString();
            _statMine.Text = all.Count(p => p.DoctorId == _doctor.Id).ToString();
            _statToday.Text = all.Count(p => p.UsageDate.Date == DateTime.Today).ToString();
            _statDrugs.Text = _store.Data.Drugs.Count.ToString();

            _recentGrid.Rows.Clear();
            foreach (var p in all.OrderByDescending(p => p.UsageDate).Take(10))
            {
                _recentGrid.Rows.Add(p.FullName, p.Department,
                    p.UsageDate.ToString("dd.MM.yyyy"), p.DrugsSummary(),
                    repo.DoctorLabel(p.DoctorId));
            }
        }

        private void OnLoadDemo(object sender, EventArgs e)
        {
            var answer = MessageBox.Show(this,
                "Загрузить 15 демонстрационных карточек?\nОни будут привязаны к вашему аккаунту.",
                "Демо-данные", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
                return;

            int added = DemoData.Load(_store, _doctor.Id);
            RefreshDashboard();
            MessageBox.Show(this, "Загружено карточек: " + added,
                "Демо-данные", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
