using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Forms
{
    /// <summary>Резервные копии: создание, просмотр списка, восстановление.</summary>
    public class BackupForm : Form
    {
        private readonly BackupService _backup;
        private readonly ListBox _list;

        public BackupForm(DataStore store, string appDir)
        {
            _backup = new BackupService(store, appDir);

            Text = "Резервные копии";
            ClientSize = new Size(620, 480);
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(480, 360);
            BackColor = Ui.ContentBg;
            Font = Ui.BaseFont;

            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(12, 8, 12, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Ui.ContentBg
            };

            var btnCreate = Ui.ActionButton("Создать копию");
            var btnRestore = Ui.ActionButton("Восстановить выбранную", false);
            var btnOpenDir = Ui.ActionButton("Открыть папку", false);

            btnCreate.Click += OnCreate;
            btnRestore.Click += OnRestore;
            btnOpenDir.Click += (s, e) => OpenBackupDir();

            top.Controls.Add(btnCreate);
            top.Controls.Add(btnRestore);
            top.Controls.Add(btnOpenDir);

            _list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Font = Ui.BaseFont };

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 4), BackColor = Ui.ContentBg };
            body.Controls.Add(_list);

            var hint = new Label
            {
                Text = "Копии включают карточки, справочники и аккаунты врачей.",
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(110, 110, 110)
            };

            Controls.Add(body);
            Controls.Add(top);
            Controls.Add(hint);

            RefreshList();
        }

        private void RefreshList()
        {
            _list.Items.Clear();
            foreach (var path in _backup.ListBackups())
                _list.Items.Add(Path.GetFileName(path));
        }

        private void OnCreate(object sender, EventArgs e)
        {
            try
            {
                string path = _backup.CreateBackup();
                RefreshList();
                MessageBox.Show(this, "Копия создана:\n" + path, "Резервные копии",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось создать копию:\n" + ex.Message, "Резервные копии",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnRestore(object sender, EventArgs e)
        {
            if (_list.SelectedItem == null)
            {
                MessageBox.Show(this, "Сначала выберите копию в списке.", "Резервные копии",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var answer = MessageBox.Show(this,
                "Текущие данные будут заменены данными из копии.\nПродолжить?",
                "Восстановление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes)
                return;

            try
            {
                string path = Path.Combine(_backup.BackupDir, _list.SelectedItem.ToString());
                _backup.Restore(path);
                MessageBox.Show(this,
                    "Данные восстановлены.\nПерезапустите программу, чтобы войти заново.",
                    "Резервные копии", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось восстановить копию:\n" + ex.Message,
                    "Резервные копии", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenBackupDir()
        {
            try
            {
                Directory.CreateDirectory(_backup.BackupDir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _backup.BackupDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось открыть папку:\n" + ex.Message,
                    "Резервные копии", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
