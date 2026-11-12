using System;
using System.Drawing;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Справочники отделений и препаратов. Изменения сохраняются сразу.</summary>
    public class DirectoriesForm : Form
    {
        private readonly DataStore _store;

        private readonly ListBox _departmentsList;
        private readonly ListBox _drugsList;
        private readonly TextBox _departmentText;
        private readonly TextBox _drugText;

        public DirectoriesForm(DataStore store)
        {
            _store = store;

            Text = "Справочники";
            ClientSize = new Size(660, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(560, 400);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(14)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            var depBox = new GroupBox { Text = "Отделения", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };
            var drugBox = new GroupBox { Text = "Препараты", Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 10) };

            _departmentsList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _drugsList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };

            var depBottom = new Panel { Dock = DockStyle.Bottom, Height = 76 };
            _departmentText = new TextBox { Dock = DockStyle.Top, Height = 28 };
            var depButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight };
            var btnDepAdd = Ui.ActionButton("Добавить");
            var btnDepDel = Ui.ActionButton("Удалить", false);
            btnDepAdd.Click += (s, e) => AddItem(_store.Data.Departments, _departmentText, _departmentsList);
            btnDepDel.Click += (s, e) => RemoveItem(_store.Data.Departments, _departmentsList);
            depButtons.Controls.Add(btnDepAdd);
            depButtons.Controls.Add(btnDepDel);
            depBottom.Controls.Add(depButtons);
            depBottom.Controls.Add(_departmentText);

            var drugBottom = new Panel { Dock = DockStyle.Bottom, Height = 76 };
            _drugText = new TextBox { Dock = DockStyle.Top, Height = 28 };
            var drugButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.LeftToRight };
            var btnDrugAdd = Ui.ActionButton("Добавить");
            var btnDrugDel = Ui.ActionButton("Удалить", false);
            btnDrugAdd.Click += (s, e) => AddItem(_store.Data.Drugs, _drugText, _drugsList);
            btnDrugDel.Click += (s, e) => RemoveItem(_store.Data.Drugs, _drugsList);
            drugButtons.Controls.Add(btnDrugAdd);
            drugButtons.Controls.Add(btnDrugDel);
            drugBottom.Controls.Add(drugButtons);
            drugBottom.Controls.Add(_drugText);

            depBox.Controls.Add(_departmentsList);
            depBox.Controls.Add(depBottom);
            drugBox.Controls.Add(_drugsList);
            drugBox.Controls.Add(drugBottom);

            layout.Controls.Add(depBox, 0, 0);
            layout.Controls.Add(drugBox, 1, 0);
            Controls.Add(layout);

            var hint = new Label
            {
                Text = "Изменения сохраняются автоматически.",
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(110, 110, 110)
            };
            Controls.Add(hint);

            RefreshLists();
        }

        private void RefreshLists()
        {
            _departmentsList.Items.Clear();
            foreach (var d in _store.Data.Departments)
                _departmentsList.Items.Add(d);
            _drugsList.Items.Clear();
            foreach (var d in _store.Data.Drugs)
                _drugsList.Items.Add(d);
        }

        private void AddItem(System.Collections.Generic.List<string> list, TextBox input, ListBox view)
        {
            string value = (input.Text ?? "").Trim();
            if (value.Length == 0)
                return;
            if (list.Contains(value))
            {
                MessageBox.Show(this, "Такая запись уже есть.", "Справочники",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            list.Add(value);
            _store.Save();
            input.Clear();
            RefreshLists();
        }

        private void RemoveItem(System.Collections.Generic.List<string> list, ListBox view)
        {
            if (view.SelectedItem == null)
            {
                MessageBox.Show(this, "Сначала выберите запись в списке.", "Справочники",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            list.Remove(view.SelectedItem.ToString());
            _store.Save();
            RefreshLists();
        }
    }
}
