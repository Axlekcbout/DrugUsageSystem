using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Список карточек пациентов с фильтрами и кнопками добавить/изменить/удалить.</summary>
    public class PatientListForm : Form
    {
        private readonly DataStore _store;
        private readonly Doctor _doctor;
        private readonly PatientRepository _repo;
        private readonly ReportRepository _reports;

        private readonly DataGridView _grid;
        private readonly TextBox _search;
        private readonly CheckBox _onlyOwn;
        private readonly ComboBox _department;
        private readonly ComboBox _sort;
        private readonly CheckBox _useFrom;
        private readonly CheckBox _useTo;
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;
        private readonly Label _count;

        public PatientListForm(DataStore store, Doctor doctor)
        {
            _store = store;
            _doctor = doctor;
            _repo = new PatientRepository(store);
            _reports = new ReportRepository(store);

            Text = "Пациенты";
            ClientSize = new Size(1020, 620);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(860, 500);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            // -------- верхняя панель: действия + фильтры --------
            var top = new Panel { Dock = DockStyle.Top, Height = 100, Padding = new Padding(12, 8, 12, 4), BackColor = Ui.ContentBg };

            var rowActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Ui.ContentBg };
            var btnAdd = Ui.ActionButton("Добавить");
            var btnEdit = Ui.ActionButton("Изменить", false);
            var btnDelete = Ui.ActionButton("Удалить", false);
            var btnAssign = Ui.ActionButton("Передать врачу", false);
            btnAdd.Click += OnAdd;
            btnEdit.Click += OnEdit;
            btnDelete.Click += OnDelete;
            btnAssign.Click += OnAssign;

            var searchLbl = new Label { Text = "Поиск:", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _search = new TextBox { Width = 200, Margin = new Padding(3, 6, 3, 3) };
            _search.TextChanged += (s, e) => LoadData();

            rowActions.Controls.Add(btnAdd);
            rowActions.Controls.Add(btnEdit);
            rowActions.Controls.Add(btnDelete);
            rowActions.Controls.Add(btnAssign);
            rowActions.Controls.Add(searchLbl);
            rowActions.Controls.Add(_search);

            var rowFilters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Ui.ContentBg };

            var deptLbl = new Label { Text = "Отделение:", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            _department = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(3, 6, 3, 3) };
            _department.Items.Add("Все");
            foreach (var d in store.Data.Departments)
                _department.Items.Add(d);
            _department.SelectedIndex = 0;
            _department.SelectedIndexChanged += (s, e) => LoadData();

            _useFrom = new CheckBox { Text = "Период с", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _from = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Enabled = false, Value = DateTime.Today.AddMonths(-1), Margin = new Padding(3, 6, 3, 3) };
            _useFrom.CheckedChanged += (s, e) => { _from.Enabled = _useFrom.Checked; LoadData(); };
            _from.ValueChanged += (s, e) => LoadData();

            _useTo = new CheckBox { Text = "по", AutoSize = true, Margin = new Padding(12, 10, 3, 3) };
            _to = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Enabled = false, Value = DateTime.Today, Margin = new Padding(3, 6, 3, 3) };
            _useTo.CheckedChanged += (s, e) => { _to.Enabled = _useTo.Checked; LoadData(); };
            _to.ValueChanged += (s, e) => LoadData();

            _onlyOwn = new CheckBox { Text = "Только мои", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _onlyOwn.CheckedChanged += (s, e) => LoadData();

            var sortLbl = new Label { Text = "Сортировка:", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _sort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170, Margin = new Padding(3, 6, 3, 3) };
            _sort.Items.Add("по ФИО");
            _sort.Items.Add("по дате использования");
            _sort.Items.Add("по препарату");
            _sort.SelectedIndex = 0;
            _sort.SelectedIndexChanged += (s, e) => LoadData();

            var btnReset = Ui.ActionButton("Сбросить", false);
            btnReset.MinimumSize = new Size(100, 32);
            btnReset.Margin = new Padding(18, 4, 3, 3);
            btnReset.Click += (s, e) => ResetFilters();

            rowFilters.Controls.Add(deptLbl);
            rowFilters.Controls.Add(_department);
            rowFilters.Controls.Add(_useFrom);
            rowFilters.Controls.Add(_from);
            rowFilters.Controls.Add(_useTo);
            rowFilters.Controls.Add(_to);
            rowFilters.Controls.Add(_onlyOwn);
            rowFilters.Controls.Add(sortLbl);
            rowFilters.Controls.Add(_sort);
            rowFilters.Controls.Add(btnReset);

            top.Controls.Add(rowFilters);
            top.Controls.Add(rowActions);

            // -------- таблица --------
            _grid = Ui.Grid();
            var colId = new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "Id", Visible = false };
            _grid.Columns.Add(colId);
            _grid.Columns.Add("colName", "ФИО");
            _grid.Columns.Add("colBirth", "Дата рождения");
            _grid.Columns.Add("colDept", "Отделение");
            _grid.Columns.Add("colUsage", "Дата использования");
            _grid.Columns.Add("colDrugs", "Препараты");
            _grid.Columns.Add("colOwner", "Зарегистрировал");
            _grid.Columns[1].FillWeight = 24;
            _grid.Columns[2].FillWeight = 12;
            _grid.Columns[3].FillWeight = 13;
            _grid.Columns[4].FillWeight = 14;
            _grid.Columns[5].FillWeight = 23;
            _grid.Columns[6].FillWeight = 14;
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) OnEdit(s, e); };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 4), BackColor = Ui.ContentBg };
            body.Controls.Add(_grid);

            _count = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(_count);

            LoadData();
        }

        private void ResetFilters()
        {
            _search.Text = "";
            _department.SelectedIndex = 0;
            _useFrom.Checked = false;
            _useTo.Checked = false;
            _onlyOwn.Checked = false;
            _sort.SelectedIndex = 0;
            LoadData();
        }

        private void LoadData()
        {
            string needle = (_search.Text ?? "").Trim().ToLowerInvariant();
            DateTime? from = _useFrom.Checked ? _from.Value.Date : (DateTime?)null;
            DateTime? to = _useTo.Checked ? _to.Value.Date : (DateTime?)null;

            var rows = _reports.Filter(_doctor.Id, _onlyOwn.Checked, from, to,
                _department.SelectedItem?.ToString(), "Все");

            // Сортировка
            IEnumerable<StoredPatient> ordered;
            switch (_sort.SelectedIndex)
            {
                case 1:
                    ordered = rows.OrderByDescending(p => p.UsageDate).ThenBy(p => p.FullName);
                    break;
                case 2:
                    ordered = rows.OrderBy(p => p.Drugs.Count == 0)
                                  .ThenBy(p => p.Drugs.Count > 0 ? p.Drugs[0].DrugName : "")
                                  .ThenBy(p => p.FullName);
                    break;
                default:
                    ordered = rows.OrderBy(p => p.FullName);
                    break;
            }

            _grid.Rows.Clear();
            int shown = 0;
            foreach (var p in ordered)
            {
                if (needle.Length > 0 &&
                    !p.FullName.ToLowerInvariant().Contains(needle) &&
                    !p.Department.ToLowerInvariant().Contains(needle) &&
                    !p.DrugsSummary().ToLowerInvariant().Contains(needle))
                    continue;

                _grid.Rows.Add(p.Id, p.FullName, p.BirthDate.ToString("dd.MM.yyyy"),
                    p.Department, p.UsageDate.ToString("dd.MM.yyyy"),
                    p.DrugsSummary(), _repo.DoctorLabel(p.DoctorId));
                shown++;
            }
            _count.Text = "Показано карточек: " + shown;
        }

        private int? SelectedId()
        {
            if (_grid.SelectedRows.Count == 0)
                return null;
            return Convert.ToInt32(_grid.SelectedRows[0].Cells["colId"].Value);
        }

        private void OnAdd(object sender, EventArgs e)
        {
            using (var f = new PatientEditForm(_store, _doctor, null))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                    LoadData();
            }
        }

        private void OnEdit(object sender, EventArgs e)
        {
            int? id = SelectedId();
            if (id == null)
            {
                MessageBox.Show(this, "Сначала выберите карточку в таблице.", "Пациенты",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var patient = _repo.Find(id.Value);
            if (patient == null)
                return;

            using (var f = new PatientEditForm(_store, _doctor, patient))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                    LoadData();
            }
        }

        private void OnAssign(object sender, EventArgs e)
        {
            int? id = SelectedId();
            if (id == null)
            {
                MessageBox.Show(this, "Сначала выберите карточку в таблице.", "Передача карточки",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var patient = _repo.Find(id.Value);
            if (patient == null)
                return;

            // Варианты: «Общая» + все аккаунты, кроме текущего владельца
            var targets = _store.Data.Doctors.OrderBy(d => d.Login).ToList();
            var items = new List<string> { "Общая (видна всем врачам)" };
            foreach (var d in targets)
                items.Add(d.Login + " — " + d.DisplayName + " (" + d.DisplayRole + ")");

            if (!InputDialog.Ask(this, "Передать карточку",
                    new[] { "Кому передать «" + patient.FullName + "» (сейчас: " + _repo.DoctorLabel(patient.DoctorId) + ")" },
                    new[] { false }, 0, items.ToArray(), out string[] values))
                return;

            int newDoctorId = 0;
            string chosen = (values[0] ?? "").Trim();
            if (!chosen.StartsWith("Общая"))
            {
                // Логин — первая часть до « — », пробелов в логине быть не может
                string login = chosen.Split(' ')[0];
                var target = targets.FirstOrDefault(d => d.Login == login);
                if (target == null)
                {
                    MessageBox.Show(this, "Выберите врача из списка.", "Передача карточки",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                newDoctorId = target.Id;
            }

            if (!_repo.Reassign(patient.Id, newDoctorId, _doctor, out string error))
            {
                MessageBox.Show(this, error, "Передача карточки", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoadData();
            MessageBox.Show(this,
                "Карточка «" + patient.FullName + "» передана: " + _repo.DoctorLabel(newDoctorId) + ".",
                "Передача карточки", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnDelete(object sender, EventArgs e)
        {
            int? id = SelectedId();
            if (id == null)
            {
                MessageBox.Show(this, "Сначала выберите карточку в таблице.", "Пациенты",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var patient = _repo.Find(id.Value);
            if (patient == null)
                return;

            var answer = MessageBox.Show(this,
                "Удалить карточку пациента «" + patient.FullName + "»?",
                "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;

            _repo.Delete(id.Value);
            LoadData();
        }
    }
}
