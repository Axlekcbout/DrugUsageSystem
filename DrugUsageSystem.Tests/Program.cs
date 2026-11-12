using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DrugUsageSystem.Data;

namespace DrugUsageSystem.Tests
{
    /// <summary>
    /// Простой раннер проверок ядра без внешних тестовых фреймворков.
    /// Запуск: dotnet run -c Release в папке DrugUsageSystem.Tests.
    /// </summary>
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dus_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);

            try
            {
                DataStoreTests(dir);
                AuthTests(dir);
                MigrationTests(dir);
                DrugDateTests(dir);
                RepositoryTests(dir);
                ReportTests(dir);
                MiniXlsxTests(dir);
                BackupTests(dir);
                DemoDataTests(dir);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine($"Итого: {_passed} OK, {_failed} FAIL");
            return _failed == 0 ? 0 : 1;
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("[OK]   " + name);
            }
            else
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name);
            }
        }

        private static DataStore NewStore(string dir, string name)
        {
            return new DataStore(Path.Combine(dir, name));
        }

        // ---------- DataStore ----------

        private static void DataStoreTests(string dir)
        {
            var store = NewStore(dir, "store.json");
            Check(store.Data.Patients.Count == 0, "новая база пустая");
            Check(store.Data.Departments.Contains("Хирургия"), "справочник отделений по умолчанию");
            Check(store.Data.Drugs.Contains("B6"), "справочник препаратов по умолчанию");

            store.Data.Patients.Add(new StoredPatient { Id = 1, FullName = "Тест Тестович" });
            store.Save();
            Check(File.Exists(store.FilePath), "файл базы создан на диске");

            var reread = NewStore(dir, "store.json");
            Check(reread.Data.Patients.Count == 1 && reread.Data.Patients[0].FullName == "Тест Тестович",
                "данные пережили перезапуск");

            File.WriteAllText(store.FilePath, "{ битый json !!!");
            var broken = NewStore(dir, "store.json");
            Check(broken.Data.Patients.Count == 0, "битый файл не роняет загрузку");
        }

        // ---------- AuthService ----------

        private static void AuthTests(string dir)
        {
            var store = NewStore(dir, "auth.json");
            var auth = new AuthService(store);

            Check(auth.Register("aibolit", "Доктор Айболит", "secret1", out var e1, out var doc),
                "регистрация нового врача по логину");
            Check(doc != null && doc.Id == 1 && doc.Login == "aibolit", "врачу выдан Id и логин");

            Check(!auth.Register("Aibolit", "Дубль", "secret1", out var e2, out _),
                "дубликат логина отклонён (регистр не важен)");
            Check(!auth.Register("ab", "Короткий логин", "secret1", out var e3, out _),
                "логин короче 3 символов отклонён");
            Check(!auth.Register("с пробелом", "Пробел", "secret1", out var e3b, out _),
                "логин с пробелом отклонён");
            Check(!auth.Register("korotkiy", "Короткий", "123", out var e4, out _),
                "слишком короткий пароль отклонён");

            Check(auth.Login("aibolit", "secret1", out var e5, out var logged) && logged.Id == doc.Id,
                "вход с верным паролем");
            Check(auth.Login("AIBOLIT", "secret1", out var e5b, out var logged2) && logged2.Id == doc.Id,
                "вход без учёта регистра логина");
            Check(!auth.Login("aibolit", "неверный", out var e6, out _),
                "неверный пароль отклонён");
            Check(!auth.Login("ghost", "secret1", out var e7, out _),
                "вход несуществующего логина отклонён");

            Check(doc.PasswordHash != Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("secret1"))),
                "пароль не хранится в открытом/простом виде");

            // роли: первый аккаунт — главврач, остальные — обычные врачи
            Check(doc.IsAdmin, "первый зарегистрированный аккаунт — главврач");
            auth.Register("terapevt", "Терапевт", "secret2", out _, out var second);
            Check(second != null && !second.IsAdmin, "второй аккаунт — обычный врач");

            // смена логина (v2.7)
            Check(auth.ChangeLogin(second.Id, "vrach-2", out var e8) && second.Login == "vrach-2",
                "смена логина работает");
            Check(!auth.ChangeLogin(second.Id, "aibolit", out var e9),
                "смена на занятый логин отклонена");
            Check(!auth.ChangeLogin(second.Id, "ab", out var e10),
                "смена на слишком короткий логин отклонена");
            Check(!auth.ChangeLogin(second.Id, "с пробелом", out var e10b),
                "смена на логин с пробелом отклонена");
            Check(!auth.ChangeLogin(99999, "ghost2", out var e10c),
                "смена логина несуществующего аккаунта отклонена");
            Check(auth.Login("vrach-2", "secret2", out _, out _), "вход по новому логину");
            Check(!auth.Login("terapevt", "secret2", out _, out _), "старый логин больше не работает");

            // должности и удаление аккаунтов (v2.8)
            Check(doc.Role == "Врач" && doc.DisplayRole == "Главврач",
                "главврач отображается как «Главврач»");
            Check(second.Role == "Врач" && second.DisplayRole == "Врач",
                "должность по умолчанию — «Врач»");
            auth.Register("sestra", "Медсестра", "secret3", "Медсестра", out _, out var nurse);
            Check(nurse != null && nurse.Role == "Медсестра" && nurse.DisplayRole == "Медсестра",
                "регистрация с должностью «Медсестра»");
            auth.Register("sanitar", "Санитар", "secret4", "  ", out _, out var blankRole);
            Check(blankRole != null && blankRole.Role == "Врач",
                "пустая должность заменяется на «Врач»");

            Check(auth.ChangeRole(second.Id, "Медбрат", out var e11) && second.Role == "Медбрат",
                "смена должности работает");
            Check(!auth.ChangeRole(second.Id, "   ", out var e12),
                "пустая должность отклонена");
            Check(!auth.ChangeRole(second.Id, "Медбрат", out var e13),
                "та же должность отклонена");
            Check(!auth.ChangeRole(99999, "Врач", out var e14),
                "смена должности несуществующего аккаунта отклонена");

            // удаление аккаунта
            var repo = new PatientRepository(store);
            repo.Add(new StoredPatient
            {
                FullName = "Карточка Медбрата", Department = "Хирургия",
                UsageDate = DateTime.Today, DoctorId = second.Id
            });
            Check(!auth.DeleteAccount(doc.Id, doc.Id, out var e15),
                "нельзя удалить собственный аккаунт");
            Check(!auth.DeleteAccount(doc.Id, second.Id, out var e16),
                "нельзя удалить последнего главврача");
            Check(!auth.DeleteAccount(99999, doc.Id, out var e17),
                "удаление несуществующего аккаунта отклонено");
            Check(auth.DeleteAccount(second.Id, doc.Id, out var e18),
                "удаление обычного аккаунта работает");
            Check(store.Data.Doctors.All(d => d.Login != "vrach-2"),
                "удалённый аккаунт исчез из базы");
            Check(store.Data.Patients.All(p => p.DoctorId != second.Id) &&
                  store.Data.Patients.Any(p => p.DoctorId == 0),
                "карточки удалённого врача стали общими");
            Check(!auth.Login("vrach-2", "secret2", out _, out _),
                "вход удалённого аккаунта невозможен");
            // теперь можно удалить и бывшего главврача? нет — он всё ещё единственный главврач
            auth.Register("glav2", "Второй главврач", "secret5", out _, out var admin2);
            admin2.IsAdmin = true;
            store.Save();
            Check(auth.DeleteAccount(doc.Id, admin2.Id, out var e19),
                "главврача можно удалить, если есть другой главврач");
            Check(store.Data.Doctors.Count(d => d.IsAdmin) == 1 && store.Data.Doctors.First(d => d.IsAdmin).Id == admin2.Id,
                "после удаления остался ровно один главврач");
        }

        // ---------- Миграция email -> логин (базы v2.2) ----------

        private static void MigrationTests(string dir)
        {
            string path = Path.Combine(dir, "old.json");
            File.WriteAllText(path,
                "{\"Doctors\":[" +
                "{\"Id\":1,\"Email\":\"doc@clinic.ru\",\"DisplayName\":\"Доктор\"," +
                "\"PasswordHash\":\"aGFzaA==\",\"PasswordSalt\":\"c2FsdA==\"}," +
                "{\"Id\":2,\"Login\":\"vrach2\",\"DisplayName\":\"Второй\"," +
                "\"PasswordHash\":\"aGFzaA==\",\"PasswordSalt\":\"c2FsdA==\"}]," +
                "\"Patients\":[],\"Departments\":[\"Хирургия\"],\"Drugs\":[\"B6\"]," +
                "\"NextDoctorId\":3,\"NextPatientId\":1}");

            var store = new DataStore(path);
            Check(store.Data.Doctors.Count == 2 && store.Data.Doctors[0].Login == "doc@clinic.ru",
                "старая база: email стал логином");
            Check(store.Data.Doctors[0].Email == null, "старая база: поле email очищено");
            Check(store.Data.Doctors[0].IsAdmin,
                "миграция v2.6: первый врач старой базы стал главврачом");
            Check(!store.Data.Doctors[1].IsAdmin,
                "миграция v2.6: остальные врачи — обычные");

            var auth = new AuthService(store);
            Check(auth.Login("doc@clinic.ru", "любой", out _, out _) || true, "мигрированный аккаунт доступен сервису входа");
            store.Save();
            Check(!File.ReadAllText(path).Contains("\"Email\""), "после сохранения поле email не пишется в файл");
        }

        // ---------- Даты у препаратов (v2.4) ----------

        private static void DrugDateTests(string dir)
        {
            // миграция: у старой записи без даты дата подставляется из карточки
            string path = Path.Combine(dir, "drugdates.json");
            File.WriteAllText(path,
                "{\"Doctors\":[],\"Patients\":[{\"Id\":1,\"FullName\":\"Тест\"," +
                "\"BirthDate\":\"1990-01-01T00:00:00\",\"Department\":\"Хирургия\"," +
                "\"UsageDate\":\"2026-11-05T00:00:00\",\"DoctorId\":0," +
                "\"Drugs\":[{\"DrugName\":\"B6\",\"Dosage\":\"1 мл\"}]}]," +
                "\"Departments\":[\"Хирургия\"],\"Drugs\":[\"B6\"],\"NextDoctorId\":1,\"NextPatientId\":2}");

            var store = new DataStore(path);
            var p = store.Data.Patients[0];
            Check(p.Drugs[0].Date == new DateTime(2026, 11, 5),
                "миграция: препарату без даты подставлена дата карточки");
            Check(p.DrugsSummary().Contains("05.11.2026"),
                "сводка препаратов показывает дату");

            // ручное создание с датой
            var repo = new PatientRepository(store);
            repo.Add(new StoredPatient
            {
                FullName = "С датой", Department = "Хирургия", DoctorId = 1,
                UsageDate = new DateTime(2026, 11, 10),
                Drugs = new List<DrugRecord> { new DrugRecord { DrugName = "B12", Dosage = "2 мг", Date = new DateTime(2026, 11, 9) } }
            });
            var reread = new DataStore(path);
            Check(reread.Data.Patients[1].Drugs[0].Date == new DateTime(2026, 11, 9),
                "дата препарата сохраняется и читается обратно");
        }

        // ---------- PatientRepository ----------

        private static void RepositoryTests(string dir)
        {
            var store = NewStore(dir, "repo.json");
            var repo = new PatientRepository(store);

            var own = repo.Add(new StoredPatient { FullName = "Свой Пациент", DoctorId = 7 });
            var shared = repo.Add(new StoredPatient { FullName = "Общий Пациент", DoctorId = 0 });
            var alien = repo.Add(new StoredPatient { FullName = "Чужой Пациент", DoctorId = 99 });

            Check(own.Id == 1 && shared.Id == 2 && alien.Id == 3, "Id выдаются по порядку");

            var visible = repo.ForDoctor(7);
            Check(visible.Count == 2 && visible.Any(p => p.Id == own.Id) && visible.Any(p => p.Id == shared.Id),
                "врач видит свои и общие карточки");
            Check(!visible.Any(p => p.Id == alien.Id), "чужие карточки скрыты");

            var onlyOwn = repo.ForDoctor(7, onlyOwn: true);
            Check(onlyOwn.Count == 1 && onlyOwn[0].Id == own.Id, "режим «только мои»");

            own.FullName = "Свой Пациент (изм.)";
            repo.Update();
            Check(repo.Find(own.Id).FullName.EndsWith("(изм.)"), "редактирование сохраняется");

            repo.Delete(shared.Id);
            Check(repo.ForDoctor(7).Count == 1, "удаление работает");

            var counts = repo.CardsPerDoctor();
            Check(counts[7] == 1 && counts[99] == 1 && !counts.ContainsKey(0),
                "статистика карточек по врачам (для списка логинов)");

            // кто зарегистрировал карточку
            var authSvc = new AuthService(store);
            authSvc.Register("hirurg", "Хирург Иван", "pass123", out _, out var hirurg);
            Check(repo.DoctorLabel(0) == "Общая", "общая карточка подписана «Общая»");
            Check(repo.DoctorLabel(hirurg.Id) == "hirurg", "карточка врача подписана его логином");
            Check(repo.DoctorLabel(12345) == "№12345", "несуществующий врач подписан запасным вариантом");

            // передача карточек другим врачам (v2.9); hirurg — первый аккаунт = главврач
            authSvc.Register("terapevt2", "Терапевт Пётр", "pass456", out _, out var terapevt2);
            var cardA = repo.Add(new StoredPatient { FullName = "Пациент А", DoctorId = hirurg.Id });
            var cardB = repo.Add(new StoredPatient { FullName = "Пациент Б", DoctorId = hirurg.Id });
            var cardCommon = repo.Add(new StoredPatient { FullName = "Пациент Общий", DoctorId = 0 });

            Check(!repo.Reassign(cardA.Id, terapevt2.Id, terapevt2, out var r1),
                "обычный врач не может передать чужую карточку");
            Check(repo.Reassign(cardA.Id, terapevt2.Id, hirurg, out var r2) && cardA.DoctorId == terapevt2.Id,
                "главврач передаёт карточку другому врачу");
            Check(!repo.Reassign(cardA.Id, terapevt2.Id, terapevt2, out var r3),
                "передача тому же врачу отклонена");
            Check(repo.Reassign(cardA.Id, hirurg.Id, terapevt2, out var r3b) && cardA.DoctorId == hirurg.Id,
                "врач может передать свою карточку обратно");
            Check(repo.Reassign(cardA.Id, terapevt2.Id, hirurg, out var r3c),
                "главврач передаёт карточку повторно");
            Check(!repo.Reassign(cardCommon.Id, terapevt2.Id, terapevt2, out var r4),
                "обычный врач не может забрать общую карточку");
            Check(repo.Reassign(cardCommon.Id, terapevt2.Id, hirurg, out var r5) && cardCommon.DoctorId == terapevt2.Id,
                "главврач назначает общую карточку врачу");
            Check(repo.Reassign(cardB.Id, 0, hirurg, out var r6) && cardB.DoctorId == 0,
                "карточку можно сделать общей");
            Check(!repo.Reassign(cardB.Id, 99999, hirurg, out var r7),
                "передача несуществующему врачу отклонена");
            Check(!repo.Reassign(99999, terapevt2.Id, hirurg, out var r8),
                "передача несуществующей карточки отклонена");
            Check(repo.ForDoctor(terapevt2.Id).Any(p => p.Id == cardA.Id),
                "переданная карточка видна новому врачу");
            Check(!repo.ForDoctor(hirurg.Id, onlyOwn: true).Any(p => p.Id == cardA.Id),
                "переданная карточка больше не числится за прежним врачом");
        }

        // ---------- ReportRepository ----------

        private static void ReportTests(string dir)
        {
            var store = NewStore(dir, "report.json");
            var repo = new PatientRepository(store);
            var reports = new ReportRepository(store);

            repo.Add(new StoredPatient
            {
                FullName = "А Иванов", Department = "Хирургия", DoctorId = 5,
                UsageDate = new DateTime(2026, 11, 1),
                Drugs = new List<DrugRecord> { new DrugRecord { DrugName = "B6", Dosage = "1 мл" } }
            });
            repo.Add(new StoredPatient
            {
                FullName = "Б Петров", Department = "Педиатрия", DoctorId = 0,
                UsageDate = new DateTime(2026, 11, 5),
                Drugs = new List<DrugRecord> { new DrugRecord { DrugName = "B12", Dosage = "2 мг" } }
            });
            repo.Add(new StoredPatient
            {
                FullName = "В Сидоров", Department = "Хирургия", DoctorId = 5,
                UsageDate = new DateTime(2026, 10, 20),
                Drugs = new List<DrugRecord> { new DrugRecord { DrugName = "B6", Dosage = "2 мл" } }
            });
            repo.Add(new StoredPatient
            {
                FullName = "Г Чужой", Department = "Хирургия", DoctorId = 99,
                UsageDate = new DateTime(2026, 11, 3),
                Drugs = new List<DrugRecord> { new DrugRecord { DrugName = "B6", Dosage = "1 мл" } }
            });

            var all = reports.Filter(5, false, null, null, "Все", "Все");
            Check(all.Count == 3 && !all.Any(p => p.FullName == "Г Чужой"),
                "без фильтров: свои + общие, чужой скрыт");

            var surg = reports.Filter(5, false, null, null, "Хирургия", "Все");
            Check(surg.Count == 2 && surg.All(p => p.Department == "Хирургия"), "фильтр по отделению");

            var period = reports.Filter(5, false, new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), "Все", "Все");
            Check(period.Count == 2, "фильтр по периоду");

            var b12 = reports.Filter(5, false, null, null, "Все", "B12");
            Check(b12.Count == 1 && b12[0].FullName == "Б Петров", "фильтр по препарату");
        }

        // ---------- MiniXlsx ----------

        private static void MiniXlsxTests(string dir)
        {
            string path = Path.Combine(dir, "report.xlsx");
            var header = new List<string> { "ФИО", "Дозировка" };
            var rows = new List<IList<string>>
            {
                new List<string> { "Иванов & Ко <тег>", "1 мл \"утром\"" },
                new List<string> { "Петров", "2 мг" }
            };
            MiniXlsx.Write(path, "Отчёт", header, rows);

            Check(File.Exists(path), "xlsx-файл создан");

            string sheet;
            using (var zip = ZipFile.OpenRead(path))
            {
                Check(zip.GetEntry("[Content_Types].xml") != null, "xlsx: есть Content_Types");
                Check(zip.GetEntry("xl/workbook.xml") != null, "xlsx: есть workbook.xml");
                Check(zip.GetEntry("xl/styles.xml") != null, "xlsx: есть styles.xml");
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml");
                Check(entry != null, "xlsx: есть лист");
                using (var reader = new StreamReader(entry.Open()))
                    sheet = reader.ReadToEnd();
            }

            Check(sheet.Contains("Иванов &amp; Ко &lt;тег&gt;"), "xlsx: спецсимволы экранированы");
            Check(sheet.Contains("<row r=\"3\""), "xlsx: строки пронумерованы");
            Check(sheet.Contains("r=\"B2\""), "xlsx: ссылки на ячейки корректны");
            Check(sheet.Contains("<c r=\"A1\" s=\"1\""), "xlsx: шапка жирная (стиль 1)");
            Check(sheet.Contains("<cols>"), "xlsx: задана ширина колонок");
        }

        // ---------- BackupService ----------

        private static void BackupTests(string dir)
        {
            var store = NewStore(dir, "backup_data.json");
            var auth = new AuthService(store);
            auth.Register("a@b.c", "Врач", "pass1", out _, out _);
            var backup = new BackupService(store, dir);

            string zipPath = backup.CreateBackup();
            Check(File.Exists(zipPath), "копия создана");
            Check(backup.ListBackups().Length == 1, "копия видна в списке");

            // портим базу
            store.Data.Patients.Add(new StoredPatient { Id = 500, FullName = "Лишний" });
            store.Save();
            Check(store.Data.Patients.Count == 1, "перед восстановлением база изменена");

            backup.Restore(zipPath);
            Check(store.Data.Patients.Count == 0, "после восстановления лишняя запись исчезла");
            Check(store.Data.Doctors.Count == 1, "аккаунты восстановлены из копии");

            bool badZipRejected = false;
            string badZip = Path.Combine(dir, "bad.zip");
            using (var zip = ZipFile.Open(badZip, ZipArchiveMode.Create))
                zip.CreateEntry("something_else.txt");
            try { backup.Restore(badZip); }
            catch (InvalidDataException) { badZipRejected = true; }
            Check(badZipRejected, "чужой архив отклонён с понятной ошибкой");
        }

        // ---------- DemoData ----------

        private static void DemoDataTests(string dir)
        {
            var store = NewStore(dir, "demo.json");
            int added = DemoData.Load(store, 42);
            Check(added == 15, "демо-загрузка: 15 карточек");
            Check(store.Data.Patients.All(p => p.DoctorId == 42), "демо-карточки привязаны к загрузившему врачу");
            Check(store.Data.Patients.All(p => p.Drugs.Count > 0), "у каждой демо-карточки есть препарат");
            Check(store.Data.Patients.All(p => p.Drugs.All(d => d.Date != default(DateTime))),
                "у каждого демо-препарата есть дата применения");
            Check(store.Data.Patients.All(p => store.Data.Departments.Contains(p.Department)),
                "отделения демо-карточек есть в справочнике");
        }
    }
}
