using System.Drawing;
using System.Windows.Forms;

namespace DrugUsageSystem.Forms
{
    /// <summary>Общее оформление: цвета, шрифты, кнопки сайдбара, карточки статистики.</summary>
    internal static class Ui
    {
        public static readonly Color SidebarBg = Color.FromArgb(47, 59, 82);
        public static readonly Color SidebarHover = Color.FromArgb(62, 77, 105);
        public static readonly Color Accent = Color.FromArgb(76, 120, 168);
        public static readonly Color AccentDark = Color.FromArgb(58, 95, 138);
        public static readonly Color ContentBg = Color.FromArgb(245, 246, 250);
        public static readonly Color Danger = Color.FromArgb(192, 57, 43);

        public static readonly Font BaseFont = new Font("Segoe UI", 10f, FontStyle.Regular);
        public static readonly Font BoldFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        public static readonly Font TitleFont = new Font("Segoe UI", 14f, FontStyle.Bold);
        public static readonly Font BigNumberFont = new Font("Segoe UI", 20f, FontStyle.Bold);

        public static Button SidebarButton(string text)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 46,
                FlatStyle = FlatStyle.Flat,
                BackColor = SidebarBg,
                ForeColor = Color.White,
                Font = BaseFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 0, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = SidebarHover;
            return b;
        }

        public static Button ActionButton(string text, bool primary = true)
        {
            var b = new Button
            {
                Text = text,
                Height = 36,
                AutoSize = true,
                MinimumSize = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : Color.FromArgb(60, 60, 60),
                Font = BaseFont,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
            if (primary)
                b.FlatAppearance.MouseOverBackColor = AccentDark;
            return b;
        }

        /// <summary>Карточка статистики для дашборда. Возвращает метку со значением.</summary>
        public static Label StatCard(FlowLayoutPanel parent, string title, Color accent)
        {
            var card = new Panel
            {
                Width = 230,
                Height = 96,
                BackColor = Color.White,
                Margin = new Padding(0, 0, 16, 16),
                Padding = new Padding(14, 10, 10, 10)
            };
            var stripe = new Panel { Width = 5, Dock = DockStyle.Left, BackColor = accent };
            var titleLbl = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 26,
                Font = BaseFont,
                ForeColor = Color.FromArgb(110, 110, 110),
                TextAlign = ContentAlignment.MiddleLeft
            };
            var valueLbl = new Label
            {
                Text = "0",
                Dock = DockStyle.Fill,
                Font = BigNumberFont,
                ForeColor = Color.FromArgb(40, 40, 40),
                TextAlign = ContentAlignment.MiddleLeft
            };
            card.Controls.Add(valueLbl);
            card.Controls.Add(titleLbl);
            card.Controls.Add(stripe);
            parent.Controls.Add(card);
            return valueLbl;
        }

        public static DataGridView Grid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                Font = BaseFont,
                EnableHeadersVisualStyles = false
            };
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(236, 239, 244);
            g.ColumnHeadersDefaultCellStyle.Font = BoldFont;
            g.ColumnHeadersHeight = 34;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 252);
            g.RowTemplate.Height = 28;
            return g;
        }
    }
}
