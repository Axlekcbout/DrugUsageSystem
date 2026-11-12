using System;
using System.IO;
using System.IO.Compression;

namespace DrugUsageSystem.Data
{
    /// <summary>
    /// Резервное копирование базы (включая аккаунты врачей — они хранятся в том же JSON).
    /// Копия — обычный ZIP-архив с файлом данных внутри.
    /// </summary>
    public class BackupService
    {
        public const string DataEntryName = "drug_usage_data.json";

        private readonly DataStore _store;
        private readonly string _backupDir;

        public BackupService(DataStore store, string appDir)
        {
            _store = store;
            _backupDir = Path.Combine(appDir, "backups");
        }

        public string BackupDir => _backupDir;

        public string CreateBackup()
        {
            _store.Save(); // сначала сбрасываем всё на диск
            Directory.CreateDirectory(_backupDir);
            string path = Path.Combine(_backupDir,
                "backup_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".zip");

            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                if (File.Exists(_store.FilePath))
                    zip.CreateEntryFromFile(_store.FilePath, DataEntryName);
            }
            return path;
        }

        public void Restore(string zipPath)
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                ZipArchiveEntry entry = zip.GetEntry(DataEntryName);
                if (entry == null)
                    throw new InvalidDataException("В этом архиве нет файла данных программы.");

                string tmp = _store.FilePath + ".restore";
                entry.ExtractToFile(tmp, true);
                File.Copy(tmp, _store.FilePath, true);
                File.Delete(tmp);
            }
            _store.Reload();
        }

        public string[] ListBackups()
        {
            if (!Directory.Exists(_backupDir))
                return new string[0];
            var files = Directory.GetFiles(_backupDir, "backup_*.zip");
            Array.Sort(files);
            Array.Reverse(files); // новые сверху
            return files;
        }
    }
}
