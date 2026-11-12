using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DrugUsageSystem.Data
{
    /// <summary>Аккаунт врача (вход по логину).</summary>
    public class Doctor
    {
        public int Id { get; set; }
        public string Login { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Главврач (администратор): видит список всех аккаунтов и управляет ролями.</summary>
        public bool IsAdmin { get; set; }

        /// <summary>Должность: «Врач», «Медсестра» и т.п. У главврача всегда отображается «Главврач».</summary>
        public string Role { get; set; } = "Врач";

        /// <summary>Роль для отображения в интерфейсе.</summary>
        [JsonIgnore]
        public string DisplayRole => IsAdmin
            ? "Главврач"
            : (string.IsNullOrWhiteSpace(Role) ? "Врач" : Role);

        /// <summary>Устаревшее поле из версии 2.2 — только для миграции старых баз (вход был по email).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Email { get; set; }
    }

    /// <summary>Строка препарата в карточке пациента.</summary>
    public class DrugRecord
    {
        public string DrugName { get; set; } = "";
        public string Dosage { get; set; } = "";

        /// <summary>Дата применения препарата. default(DateTime) — дата не задана (старые данные).</summary>
        public DateTime Date { get; set; }
    }

    /// <summary>Карточка пациента. DoctorId = 0 — общая карточка, видна всем врачам.</summary>
    public class StoredPatient
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public DateTime BirthDate { get; set; } = DateTime.Today.AddYears(-30);
        public string Department { get; set; } = "";
        public DateTime UsageDate { get; set; } = DateTime.Today;
        public int DoctorId { get; set; }
        public List<DrugRecord> Drugs { get; set; } = new List<DrugRecord>();

        public string DrugsSummary()
        {
            var parts = new List<string>();
            foreach (var d in Drugs)
            {
                string s = d.DrugName;
                var extras = new List<string>();
                if (!string.IsNullOrWhiteSpace(d.Dosage))
                    extras.Add(d.Dosage);
                if (d.Date != default(DateTime))
                    extras.Add(d.Date.ToString("dd.MM.yyyy"));
                if (extras.Count > 0)
                    s += " (" + string.Join(", ", extras) + ")";
                parts.Add(s);
            }
            return string.Join(", ", parts);
        }
    }

    /// <summary>Корневой объект базы (сохраняется в JSON целиком, включая аккаунты).</summary>
    public class AppData
    {
        public List<Doctor> Doctors { get; set; } = new List<Doctor>();
        public List<StoredPatient> Patients { get; set; } = new List<StoredPatient>();
        public List<string> Departments { get; set; } = new List<string> { "Хирургия", "Педиатрия", "Реанимация" };
        public List<string> Drugs { get; set; } = new List<string> { "B6", "B12" };
        public int NextDoctorId { get; set; } = 1;
        public int NextPatientId { get; set; } = 1;
    }
}
