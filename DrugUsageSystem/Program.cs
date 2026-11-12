using System;
using System.IO;
using System.Windows.Forms;
using DrugUsageSystem.Data;
using DrugUsageSystem.Forms;

namespace DrugUsageSystem
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();

            // База хранится рядом с пользователем, а не в папке с exe —
            // ничего вручную копировать не нужно.
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DrugUsageSystem");
            Directory.CreateDirectory(dir);

            var store = new DataStore(Path.Combine(dir, "drug_usage_data.json"));

            // Цикл входа: после «Выйти из аккаунта» снова показываем окно входа.
            while (true)
            {
                Doctor doctor;
                using (var login = new LoginForm(store))
                {
                    if (login.ShowDialog() != DialogResult.OK)
                        return; // окно входа закрыто — завершаем приложение
                    doctor = login.LoggedDoctor;
                }

                using (var main = new MainForm(store, doctor))
                    main.ShowDialog();
            }
        }
    }
}
