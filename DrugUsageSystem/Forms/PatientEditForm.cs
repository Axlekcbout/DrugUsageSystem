using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Форма создания/редактирования карточки пациента.</summary>
    public class PatientEditForm : Form
    {
        private readonly DataStore _store;
        private readonly Doctor _doctor;
        private readonly StoredPatient _existing; // null — новая карточка

        private readonly TextBox _fullName;
        private readonly DateTimePicker _birthDate;
        private readonly ComboBox _department;
        private readonly DateTimePicker _usageDate;
        private readonly DataGridView _drugsGrid;

        public PatientEditForm(DataStore store, Doctor doctor, StoredPatient existing)
        {
            _store = store;
            _doctor = doctor;
            _existing = existing;

            Text = existing == null ? "Новая карточка пациента" : "Карточка пациента";
            ClientSize = new Size(540, 600);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = Ui.BaseFont;

            var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 16, 20, 12) };

            // --- Препараты (таблица) — добавляем первым, она Dock=Fill ---
            var drugsLabel = new Label { Text = "Препараты и дозировка", Dock = DockStyle.Top, Height = 24, Font = Ui.BoldFont };
            _drugsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                Font = Ui.BaseFont
            };
            var colDrug = new DataGridViewComboBoxColumn { Name = "colDrug", HeaderText = "Препарат", FlatStyle = FlatStyle.Flat };
            foreach (var d in store.Data.Drugs)
                colDrug.Items.Add(d);
            // если в карточке есть препарат, которого уже нет в справочнике — добавим в список, чтобы значение отображалось
            if (existing != null)
                foreach (var dr in existing.Drugs)
                    if (!colDrug.Items.Contains(dr.DrugName))
                        colDrug.Items.Add(dr.DrugName);
            var colDose = new DataGridViewTextBoxColumn { Name = "colDose", HeaderText = "Дозировка" };
            var colDate = new DataGridViewTextBoxColumn { Name = "colDate", HeaderText = "Дата применения" };
            _drugsGrid.Columns.Add(colDrug);
            _drugsGrid.Columns.Add(colDose);
            _drugsGrid.Columns.Add(colDate);
            colDrug.FillWeight = 40;
            colDose.FillWeight = 28;
            colDate.FillWeight = 32;

            var drugsPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 4) };
            drugsPanel.Controls.Add(_drugsGrid);

            // --- нижняя панель с кнопками ---
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            var btnSave = Ui.ActionButton("Сохранить");
            var btnCancel = Ui.ActionButton("Отмена", false);
            btnSave.Click += OnSave;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(btnSave);
            buttons.Controls.Add(btnCancel);

            // --- верхние поля ---
            var top = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 220,
                ColumnCount = 1,
                RowCount = 8
            };
            top.RowStyles.Clear();
            for (int i = 0; i < 4; i++)
            {
                top.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // подпись
                top.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // поле
            }

            _fullName = new TextBox { Dock = DockStyle.Top };
            _birthDate = new DateTimePicker { Dock = DockStyle.Top, Format = DateTimePickerFormat.Short };
            _department = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown };
            foreach (var d in store.Data.Departments)
                _department.Items.Add(d);
            _usageDate = new DateTimePicker { Dock = DockStyle.Top, Format = DateTimePickerFormat.Short };

            top.Controls.Add(MakeLabel("ФИО пациента"));
            top.Controls.Add(_fullName);
            top.Controls.Add(MakeLabel("Дата рождения"));
            top.Controls.Add(_birthDate);
            top.Controls.Add(MakeLabel("Отделение"));
            top.Controls.Add(_department);
            top.Controls.Add(MakeLabel("Дата использования"));
            top.Controls.Add(_usageDate);

            root.Controls.Add(drugsPanel);   // Fill
            root.Controls.Add(drugsLabel);   // Top (после Fill в коллекции — докируется раньше по z-order)
            root.Controls.Add(buttons);      // Bottom
            root.Controls.Add(top);          // Top — верхняя часть
            Controls.Add(root);

            FillFromExisting();

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private static Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.BottomLeft,
                ForeColor = Color.FromArgb(90, 90, 90)
            };
        }

        private void FillFromExisting()
        {
            if (_existing == null)
            {
                if (_department.Items.Count > 0)
                    _department.SelectedIndex = 0;
                _usageDate.Value = DateTime.Today;
                return;
            }

            _fullName.Text = _existing.FullName;
            _birthDate.Value = _existing.BirthDate;
            _department.Text = _existing.Department;
            _usageDate.Value = _existing.UsageDate;
            foreach (var dr in _existing.Drugs)
            {
                string dateText = dr.Date != default(DateTime)
                    ? dr.Date.ToString("dd.MM.yyyy")
                    : _existing.UsageDate.ToString("dd.MM.yyyy");
                _drugsGrid.Rows.Add(dr.DrugName, dr.Dosage, dateText);
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            string name = (_fullName.Text ?? "").Trim();
            if (name.Length == 0)
            {
                MessageBox.Show(this, "Введите ФИО пациента.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string dept = (_department.Text ?? "").Trim();
            if (dept.Length == 0)
            {
                MessageBox.Show(this, "Укажите отделение.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var drugs = new System.Collections.Generic.List<DrugRecord>();
            foreach (DataGridViewRow row in _drugsGrid.Rows)
            {
                if (row.IsNewRow)
                    continue;
                string drug = Convert.ToString(row.Cells["colDrug"].Value ?? "");
                string dose = Convert.ToString(row.Cells["colDose"].Value ?? "");
                string dateText = Convert.ToString(row.Cells["colDate"].Value ?? "");
                if (drug.Trim().Length == 0 && dose.Trim().Length == 0 && dateText.Trim().Length == 0)
                    continue;
                if (drug.Trim().Length == 0)
                {
                    MessageBox.Show(this, "В строке с дозировкой не выбран препарат.", "Проверка",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Дата применения: пусто = дата использования карточки
                DateTime drugDate = _usageDate.Value.Date;
                if (dateText.Trim().Length > 0 &&
                    !DateTime.TryParseExact(dateText.Trim(), "dd.MM.yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out drugDate))
                {
                    MessageBox.Show(this,
                        "Дата применения препарата «" + drug.Trim() + "» не понятна.\nВведите в формате дд.мм.гггг, например 10.11.2026.",
                        "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                drugs.Add(new DrugRecord { DrugName = drug.Trim(), Dosage = dose.Trim(), Date = drugDate.Date });
            }
            if (drugs.Count == 0)
            {
                MessageBox.Show(this, "Добавьте хотя бы один препарат.", "Проверка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Новое отделение из поля — сразу пополняем справочник
            if (!_store.Data.Departments.Contains(dept))
            {
                _store.Data.Departments.Add(dept);
            }

            var repo = new PatientRepository(_store);
            if (_existing == null)
            {
                repo.Add(new StoredPatient
                {
                    FullName = name,
                    BirthDate = _birthDate.Value.Date,
                    Department = dept,
                    UsageDate = _usageDate.Value.Date,
                    DoctorId = _doctor.Id,
                    Drugs = drugs
                });
            }
            else
            {
                _existing.FullName = name;
                _existing.BirthDate = _birthDate.Value.Date;
                _existing.Department = dept;
                _existing.UsageDate = _usageDate.Value.Date;
                _existing.Drugs = drugs;
                repo.Update();
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
