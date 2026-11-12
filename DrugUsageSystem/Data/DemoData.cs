using System;
using System.Collections.Generic;

namespace DrugUsageSystem.Data
{
    /// <summary>
    /// Демонстрационные данные: 15 пациентов.
    /// Карточки привязываются к врачу, который нажал кнопку загрузки.
    /// </summary>
    public static class DemoData
    {
        private static readonly (string Name, int Year, int Month, int Day)[] People =
        {
            ("Иванов Иван Петрович",      1985, 3, 12),
            ("Петрова Мария Сергеевна",   1992, 7, 4),
            ("Сидоров Алексей Николаевич",1978, 1, 25),
            ("Кузнецова Ольга Дмитриевна",2001, 11, 8),
            ("Смирнов Дмитрий Андреевич", 1969, 4, 17),
            ("Волкова Анна Викторовна",   2015, 9, 2),
            ("Фёдоров Павел Олегович",    1988, 6, 30),
            ("Морозова Елена Игоревна",   1995, 2, 14),
            ("Новиков Сергей Валерьевич", 1974, 12, 1),
            ("Захарова Дарья Павловна",   2019, 5, 22),
            ("Орлов Николай Семёнович",   1962, 8, 9),
            ("Беляева Татьяна Юрьевна",   1990, 10, 27),
            ("Громов Виктор Алексеевич",  1983, 3, 5),
            ("Тарасова Ирина Олеговна",   2007, 7, 19),
            ("Комаров Артём Денисович",   1997, 4, 11),
        };

        public static int Load(DataStore store, int doctorId)
        {
            var deps = store.Data.Departments;
            var drugs = store.Data.Drugs;
            if (deps.Count == 0) deps.Add("Терапия");
            if (drugs.Count == 0) drugs.Add("B6");

            var rnd = new Random(42);
            int added = 0;

            for (int i = 0; i < People.Length; i++)
            {
                var (name, y, m, d) = People[i];
                var patient = new StoredPatient
                {
                    Id = store.Data.NextPatientId++,
                    FullName = name,
                    BirthDate = new DateTime(y, m, d),
                    Department = deps[i % deps.Count],
                    UsageDate = DateTime.Today.AddDays(-rnd.Next(0, 30)),
                    DoctorId = doctorId,
                    Drugs = new List<DrugRecord>()
                };

                // 1–2 препарата на карточку
                int drugCount = 1 + rnd.Next(0, Math.Min(2, drugs.Count));
                var used = new HashSet<int>();
                for (int k = 0; k < drugCount; k++)
                {
                    int idx = rnd.Next(0, drugs.Count);
                    if (!used.Add(idx)) continue;
                    patient.Drugs.Add(new DrugRecord
                    {
                        DrugName = drugs[idx],
                        Dosage = (1 + rnd.Next(0, 3)) + (idx % 2 == 0 ? " мл" : " мг"),
                        Date = patient.UsageDate.AddDays(-rnd.Next(0, 3))
                    });
                }
                if (patient.Drugs.Count == 0)
                    patient.Drugs.Add(new DrugRecord { DrugName = drugs[0], Dosage = "1 мл", Date = patient.UsageDate });

                store.Data.Patients.Add(patient);
                added++;
            }

            store.Save();
            return added;
        }
    }
}
