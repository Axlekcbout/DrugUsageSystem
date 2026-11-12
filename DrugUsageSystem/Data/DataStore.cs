using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DrugUsageSystem.Data
{
    /// <summary>
    /// Хранилище данных в JSON-файле (без SQLite и внешних пакетов).
    /// Запись атомарная: сначала во временный файл, потом замена.
    /// </summary>
    public class DataStore
    {
        private readonly string _path;

        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public AppData Data { get; private set; }

        public string FilePath => _path;

        public DataStore(string path)
        {
            _path = path;
            Data = LoadFromDisk();
        }

        private AppData LoadFromDisk()
        {
            try
            {
                if (File.Exists(_path))
                {
                    string json = File.ReadAllText(_path);
                    var data = JsonSerializer.Deserialize<AppData>(json);
                    if (data != null)
                    {
                        Migrate(data);
                        return data;
                    }
                }
            }
            catch
            {
                // Повреждённый файл не должен ронять программу — начинаем с чистой базы.
            }
            return new AppData();
        }

        /// <summary>Миграция старых баз: в v2.2 вход был по email — переносим его в логин.</summary>
        private static void Migrate(AppData data)
        {
            foreach (var d in data.Doctors)
            {
                if (string.IsNullOrWhiteSpace(d.Login) && !string.IsNullOrWhiteSpace(d.Email))
                {
                    d.Login = d.Email;
                    d.Email = null;
                }
            }

            // Миграция v2.3 -> v2.4: у препаратов появилась дата применения.
            // Если даты нет — подставляем дату использования из карточки.
            foreach (var p in data.Patients)
                foreach (var drug in p.Drugs)
                    if (drug.Date == default(DateTime))
                        drug.Date = p.UsageDate;

            // Миграция v2.5 -> v2.6: появилась роль главврача.
            // Если в базе нет ни одного главврача — назначаем первого по счёту врача.
            if (data.Doctors.Count > 0 && !data.Doctors.Any(d => d.IsAdmin))
                data.Doctors.OrderBy(d => d.Id).First().IsAdmin = true;
        }

        /// <summary>Перечитать базу с диска (например, после восстановления из копии).</summary>
        public void Reload()
        {
            Data = LoadFromDisk();
        }

        public void Save()
        {
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Data, Opts));
            if (File.Exists(_path))
                File.Delete(_path);
            File.Move(tmp, _path);
        }
    }
}
