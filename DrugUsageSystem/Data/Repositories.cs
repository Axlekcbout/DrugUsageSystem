using System;
using System.Collections.Generic;
using System.Linq;

namespace DrugUsageSystem.Data
{
    /// <summary>
    /// Работа с карточками пациентов.
    /// Каждый врач видит свои карточки (DoctorId = его Id) и общие (DoctorId = 0).
    /// </summary>
    public class PatientRepository
    {
        private readonly DataStore _store;

        public PatientRepository(DataStore store)
        {
            _store = store;
        }

        public List<StoredPatient> ForDoctor(int doctorId, bool onlyOwn = false)
        {
            return _store.Data.Patients
                .Where(p => p.DoctorId == doctorId || (!onlyOwn && p.DoctorId == 0))
                .OrderBy(p => p.FullName)
                .ToList();
        }

        public StoredPatient Find(int id)
        {
            return _store.Data.Patients.FirstOrDefault(p => p.Id == id);
        }

        public StoredPatient Add(StoredPatient patient)
        {
            patient.Id = _store.Data.NextPatientId++;
            _store.Data.Patients.Add(patient);
            _store.Save();
            return patient;
        }

        public void Update()
        {
            // Объект карточки уже лежит в общем списке — достаточно сохранить.
            _store.Save();
        }

        public void Delete(int id)
        {
            _store.Data.Patients.RemoveAll(p => p.Id == id);
            _store.Save();
        }

        /// <summary>Сколько карточек у каждого врача (Id врача -> количество).</summary>
        public Dictionary<int, int> CardsPerDoctor()
        {
            return _store.Data.Patients
                .GroupBy(p => p.DoctorId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>Кто зарегистрировал карточку: логин врача, «Общая» для общих карточек.</summary>
        public string DoctorLabel(int doctorId)
        {
            if (doctorId == 0)
                return "Общая";
            var doctor = _store.Data.Doctors.FirstOrDefault(d => d.Id == doctorId);
            return doctor != null ? doctor.Login : "№" + doctorId;
        }

        /// <summary>
        /// Передача карточки другому врачу (newDoctorId = 0 — сделать общей).
        /// Обычный врач может передавать только свои карточки; главврач — любые.
        /// </summary>
        public bool Reassign(int patientId, int newDoctorId, Doctor current, out string error)
        {
            var patient = Find(patientId);
            if (patient == null)
            {
                error = "Карточка не найдена.";
                return false;
            }
            if (!current.IsAdmin && patient.DoctorId != current.Id)
            {
                error = patient.DoctorId == 0
                    ? "Общую карточку может назначить врачу только главврач."
                    : "Можно передавать только свои карточки.";
                return false;
            }
            if (newDoctorId != 0 && !_store.Data.Doctors.Any(d => d.Id == newDoctorId))
            {
                error = "Врач не найден.";
                return false;
            }
            if (patient.DoctorId == newDoctorId)
            {
                error = "Карточка уже закреплена за этим врачом.";
                return false;
            }

            patient.DoctorId = newDoctorId;
            _store.Save();
            error = "";
            return true;
        }
    }

    /// <summary>Выборки для отчётов с фильтрами по периоду, отделению и врачу.</summary>
    public class ReportRepository
    {
        private readonly DataStore _store;

        public ReportRepository(DataStore store)
        {
            _store = store;
        }

        public List<StoredPatient> Filter(int doctorId, bool onlyOwn,
                                          DateTime? from, DateTime? to,
                                          string department, string drugName)
        {
            IEnumerable<StoredPatient> q = _store.Data.Patients
                .Where(p => p.DoctorId == doctorId || (!onlyOwn && p.DoctorId == 0));

            if (from.HasValue)
                q = q.Where(p => p.UsageDate.Date >= from.Value.Date);
            if (to.HasValue)
                q = q.Where(p => p.UsageDate.Date <= to.Value.Date);
            if (!string.IsNullOrWhiteSpace(department) && department != "Все")
                q = q.Where(p => p.Department == department);
            if (!string.IsNullOrWhiteSpace(drugName) && drugName != "Все")
                q = q.Where(p => p.Drugs.Any(d => d.DrugName == drugName));

            return q.OrderBy(p => p.UsageDate).ThenBy(p => p.FullName).ToList();
        }
    }
}
