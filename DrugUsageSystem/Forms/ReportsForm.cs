using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>
    /// Отчёты: фильтры по периоду (с готовыми пресетами), отделению, препарату и врачу.
    /// Отчёт формируется сразу при открытии; итоги считаются автоматически; экспорт в XLSX.
    /// </summary>
    public class ReportsForm : Form
    {
        private readonly Doctor _doctor;
        private readonly ReportRepository _repo;
        private readonly PatientRepository _patients;

        private readonly CheckBox _useFrom;
        private readonly CheckBox _useTo;
        private readonly DateTimePicker _from;
        private readonly DateTimePicker _to;
        private readonly ComboBox _department;
        private readonly ComboBox _drug;
        private readonly CheckBox _onlyOwn;
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly Label _summary;
        private bool _presetApplying;

        private List<StoredPatient> _current = new List<StoredPatient>();

        public ReportsForm(DataStore store, Doctor doctor)
        {
            _doctor = doctor;
            _repo = new ReportRepository(store);
            _patients = new PatientRepository(store);

            Text = "Отчёты";
            ClientSize = new Size(1060, 660);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 540);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            // -------- панель фильтров (3 строки) --------
            var filters = new Panel { Dock = DockStyle.Top, Height = 150, Padding = new Padding(12, 8, 12, 4), BackColor = Ui.ContentBg };

            // строка 1: пресеты периода + свои границы
            var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Ui.ContentBg };

            var periodLbl = new Label { Text = "Период:", AutoSize = true, Margin = new Padding(3, 10, 6, 3) };
            var btnToday = PresetButton("Сегодня", () => SetPeriod(DateTime.Today, DateTime.Today));
            var btnWeek = PresetButton("7 дней");
            var btnMonth = PresetButton("30 дней");
            var btnAll = PresetButton("Всё время");
            btnWeek.Click += (s, e) => SetPeriod(DateTime.Today.AddDays(-6), DateTime.Today);
            btnMonth.Click += (s, e) => SetPeriod(DateTime.Today.AddDays(-29), DateTime.Today);
            btnAll.Click += (s, e) => SetPeriod(null, null);

            _useFrom = new CheckBox { Text = "с", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _from = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false, Value = DateTime.Today.AddMonths(-1), Margin = new Padding(3, 6, 3, 3) };
            _useFrom.CheckedChanged += (s, e) => { _from.Enabled = _useFrom.Checked; RunReport(); };
            _from.ValueChanged += (s, e) => { if (!_presetApplying) RunReport(); };

            _useTo = new CheckBox { Text = "по", AutoSize = true, Margin = new Padding(10, 10, 3, 3) };
            _to = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 105, Enabled = false, Value = DateTime.Today, Margin = new Padding(3, 6, 3, 3) };
            _useTo.CheckedChanged += (s, e) => { _to.Enabled = _useTo.Checked; RunReport(); };
            _to.ValueChanged += (s, e) => { if (!_presetApplying) RunReport(); };

            row1.Controls.Add(periodLbl);
            row1.Controls.Add(btnToday);
            row1.Controls.Add(btnWeek);
            row1.Controls.Add(btnMonth);
            row1.Controls.Add(btnAll);
            row1.Controls.Add(_useFrom);
            row1.Controls.Add(_from);
            row1.Controls.Add(_useTo);
            row1.Controls.Add(_to);

            // строка 2: отделение, препарат, «только мои»
            var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Ui.ContentBg };

            var deptLbl = new Label { Text = "Отделение:", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
            _department = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(3, 6, 3, 3) };
            _department.Items.Add("Все");
            foreach (var d in store.Data.Departments)
                _department.Items.Add(d);
            _department.SelectedIndex = 0;
            _department.SelectedIndexChanged += (s, e) => RunReport();

            var drugLbl = new Label { Text = "Препарат:", AutoSize = true, Margin = new Padding(16, 10, 3, 3) };
            _drug = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150, Margin = new Padding(3, 6, 3, 3) };
            _drug.Items.Add("Все");
            foreach (var d in store.Data.Drugs)
                _drug.Items.Add(d);
            _drug.SelectedIndex = 0;
            _drug.SelectedIndexChanged += (s, e) => RunReport();

            _onlyOwn = new CheckBox { Text = "Только мои карточки", AutoSize = true, Margin = new Padding(18, 10, 3, 3) };
            _onlyOwn.CheckedChanged += (s, e) => RunReport();

            row2.Controls.Add(deptLbl);
            row2.Controls.Add(_department);
            row2.Controls.Add(drugLbl);
            row2.Controls.Add(_drug);
            row2.Controls.Add(_onlyOwn);

            // строка 3: действия + итоги
            var row3 = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Ui.ContentBg };

            var btnExport = Ui.ActionButton("Экспорт в XLSX");
            btnExport.Click += OnExport;
            _summary = new Label { AutoSize = true, Margin = new Padding(16, 12, 3, 3), Font = Ui.BoldFont, ForeColor = Ui.SidebarBg };

            row3.Controls.Add(btnExport);
            row3.Controls.Add(_summary);

            filters.Controls.Add(row3);
            filters.Controls.Add(row2);
            filters.Controls.Add(row1);

            // -------- таблица --------
            _grid = Ui.Grid();
            _grid.Columns.Add("colName", "ФИО");
            _grid.Columns.Add("colBirth", "Дата рождения");
            _grid.Columns.Add("colDept", "Отделение");
            _grid.Columns.Add("colUsage", "Дата использования");
            _grid.Columns.Add("colDrugs", "Препараты");
            _grid.Columns.Add("colOwner", "Зарегистрировал");
            _grid.Columns[0].FillWeight = 24;
            _grid.Columns[1].FillWeight = 13;
            _grid.Columns[2].FillWeight = 13;
            _grid.Columns[3].FillWeight = 14;
            _grid.Columns[4].FillWeight = 24;
            _grid.Columns[5].FillWeight = 12;

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 4), BackColor = Ui.ContentBg };
            body.Controls.Add(_grid);

            _status = new Label
            {
                Text = "",
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            Controls.Add(body);
            Controls.Add(filters);
            Controls.Add(_status);

            // Отчёт формируется сразу при открытии
            RunReport();
        }

        private Button PresetButton(string text)
        {
            var b = Ui.ActionButton(text, false);
            b.MinimumSize = new Size(96, 32);
            b.Margin = new Padding(3, 4, 3, 3);
            return b;
        }

        private Button PresetButton(string text, Action action)
        {
            var b = PresetButton(text);
            b.Click += (s, e) => action();
            return b;
        }

        private void SetPeriod(DateTime? from, DateTime? to)
        {
            _presetApplying = true;
            _useFrom.Checked = from.HasValue;
            _useTo.Checked = to.HasValue;
            if (from.HasValue) _from.Value = from.Value;
            if (to.HasValue) _to.Value = to.Value;
            _presetApplying = false;
            RunReport();
        }

        private void RunReport()
        {
            DateTime? from = _useFrom.Checked ? _from.Value.Date : (DateTime?)null;
            DateTime? to = _useTo.Checked ? _to.Value.Date : (DateTime?)null;

            _current = _repo.Filter(_doctor.Id, _onlyOwn.Checked, from, to,
                _department.SelectedItem?.ToString(), _drug.SelectedItem?.ToString());

            _grid.Rows.Clear();
            foreach (var p in _current)
            {
                _grid.Rows.Add(p.FullName, p.BirthDate.ToString("dd.MM.yyyy"),
                    p.Department, p.UsageDate.ToString("dd.MM.yyyy"), p.DrugsSummary(),
                    _patients.DoctorLabel(p.DoctorId));
            }

            // Итоги: карточки, приёмы препаратов, разбивка по отделениям
            int drugUses = _current.Sum(p => p.Drugs.Count);
            var byDept = _current
                .GroupBy(p => p.Department)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key + " — " + g.Count());
            string deptText = string.Join(" · ", byDept);

            _summary.Text = "Карточек: " + _current.Count + "   |   Записей о препаратах: " + drugUses;
            _status.Text = deptText.Length > 0 ? "По отделениям: " + deptText : "Ничего не найдено — измените фильтры.";
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (_current.Count == 0)
            {
                MessageBox.Show(this, "По текущим фильтрам ничего не найдено — выгружать нечего.", "Экспорт",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Таблица Excel (*.xlsx)|*.xlsx";
                dlg.FileName = "Отчёт_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".xlsx";
                if (dlg.ShowDialog(this) != DialogResult.OK)
                    return;

                try
                {
                    var header = new List<string> { "ФИО", "Дата рождения", "Отделение", "Дата использования", "Препараты", "Зарегистрировал" };
                    var rows = new List<IList<string>>();
                    foreach (var p in _current)
                    {
                        rows.Add(new List<string>
                        {
                            p.FullName,
                            p.BirthDate.ToString("dd.MM.yyyy"),
                            p.Department,
                            p.UsageDate.ToString("dd.MM.yyyy"),
                            p.DrugsSummary(),
                            _patients.DoctorLabel(p.DoctorId)
                        });
                    }
                    // итоговая строка
                    rows.Add(new List<string>
                    {
                        "ИТОГО", "", "Карточек: " + _current.Count, "",
                        "Записей о препаратах: " + _current.Sum(p => p.Drugs.Count)
                    });

                    MiniXlsx.Write(dlg.FileName, "Отчёт", header, rows);
                    _status.Text = "Отчёт сохранён: " + dlg.FileName;
                    MessageBox.Show(this, "Файл сохранён:\n" + dlg.FileName, "Экспорт",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Не удалось сохранить файл:\n" + ex.Message, "Экспорт",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
