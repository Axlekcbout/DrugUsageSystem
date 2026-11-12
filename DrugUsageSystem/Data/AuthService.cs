using System;
using System.Linq;
using System.Security.Cryptography;

namespace DrugUsageSystem.Data
{
    /// <summary>Регистрация и вход врачей по логину. Пароль — PBKDF2-SHA256, 100 000 итераций.</summary>
    public class AuthService
    {
        private const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        private readonly DataStore _store;

        public AuthService(DataStore store)
        {
            _store = store;
        }

        public bool Register(string userName, string displayName, string password, out string error, out Doctor doctor)
        {
            return Register(userName, displayName, password, "Врач", out error, out doctor);
        }

        public bool Register(string userName, string displayName, string password, string role, out string error, out Doctor doctor)
        {
            doctor = null;
            userName = (userName ?? "").Trim().ToLowerInvariant();

            if (!ValidateLogin(userName, out error))
                return false;
            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                error = "Пароль должен быть не короче 4 символов.";
                return false;
            }
            if (_store.Data.Doctors.Any(d => d.Login == userName))
            {
                error = "Аккаунт с таким логином уже существует.";
                return false;
            }

            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            doctor = new Doctor
            {
                Id = _store.Data.NextDoctorId++,
                Login = userName,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim(),
                PasswordSalt = Convert.ToBase64String(salt),
                PasswordHash = Convert.ToBase64String(Hash(password, salt)),
                CreatedAt = DateTime.Now,
                // Первый зарегистрированный аккаунт автоматически становится главврачом
                IsAdmin = _store.Data.Doctors.Count == 0,
                Role = string.IsNullOrWhiteSpace(role) ? "Врач" : role.Trim()
            };
            _store.Data.Doctors.Add(doctor);
            _store.Save();
            error = "";
            return true;
        }

        public bool Login(string userName, string password, out string error, out Doctor doctor)
        {
            doctor = null;
            userName = (userName ?? "").Trim().ToLowerInvariant();

            doctor = _store.Data.Doctors.FirstOrDefault(d => d.Login == userName);
            if (doctor == null)
            {
                error = "Аккаунт с таким логином не найден.";
                return false;
            }

            byte[] expected = Convert.FromBase64String(doctor.PasswordHash);
            byte[] actual = Hash(password ?? "", Convert.FromBase64String(doctor.PasswordSalt));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                doctor = null;
                error = "Неверный пароль.";
                return false;
            }

            error = "";
            return true;
        }

        /// <summary>Смена логина существующего аккаунта (например, главврачом).</summary>
        public bool ChangeLogin(int doctorId, string newLogin, out string error)
        {
            newLogin = (newLogin ?? "").Trim().ToLowerInvariant();

            var doctor = _store.Data.Doctors.FirstOrDefault(d => d.Id == doctorId);
            if (doctor == null)
            {
                error = "Аккаунт не найден.";
                return false;
            }
            if (!ValidateLogin(newLogin, out error))
                return false;
            if (_store.Data.Doctors.Any(d => d.Id != doctorId && d.Login == newLogin))
            {
                error = "Аккаунт с таким логином уже существует.";
                return false;
            }
            if (doctor.Login == newLogin)
            {
                error = "Это уже текущий логин.";
                return false;
            }

            doctor.Login = newLogin;
            _store.Save();
            error = "";
            return true;
        }

        /// <summary>Смена должности существующего аккаунта («Врач», «Медсестра», …).</summary>
        public bool ChangeRole(int doctorId, string newRole, out string error)
        {
            var doctor = _store.Data.Doctors.FirstOrDefault(d => d.Id == doctorId);
            if (doctor == null)
            {
                error = "Аккаунт не найден.";
                return false;
            }
            newRole = (newRole ?? "").Trim();
            if (newRole.Length == 0)
            {
                error = "Должность не может быть пустой.";
                return false;
            }
            if (doctor.Role == newRole)
            {
                error = "Это уже текущая должность.";
                return false;
            }

            doctor.Role = newRole;
            _store.Save();
            error = "";
            return true;
        }

        /// <summary>
        /// Удаление аккаунта (только для главврача — проверяется в форме).
        /// Нельзя удалить самого себя и последнего главврача.
        /// Карточки удалённого врача становятся общими (DoctorId = 0).
        /// </summary>
        public bool DeleteAccount(int doctorId, int currentDoctorId, out string error)
        {
            var doctor = _store.Data.Doctors.FirstOrDefault(d => d.Id == doctorId);
            if (doctor == null)
            {
                error = "Аккаунт не найден.";
                return false;
            }
            if (doctorId == currentDoctorId)
            {
                error = "Нельзя удалить собственный аккаунт.";
                return false;
            }
            if (doctor.IsAdmin && _store.Data.Doctors.Count(d => d.IsAdmin) <= 1)
            {
                error = "Нельзя удалить последнего главврача.";
                return false;
            }

            _store.Data.Doctors.Remove(doctor);
            // Карточки пациентов удалённого врача становятся общими
            foreach (var p in _store.Data.Patients.Where(p => p.DoctorId == doctorId))
                p.DoctorId = 0;

            _store.Save();
            error = "";
            return true;
        }

        private static bool ValidateLogin(string userName, out string error)
        {
            if (userName.Length < 3)
            {
                error = "Логин должен быть не короче 3 символов.";
                return false;
            }
            if (userName.Any(char.IsWhiteSpace))
            {
                error = "Логин не должен содержать пробелы.";
                return false;
            }
            error = "";
            return true;
        }

        private static byte[] Hash(string password, byte[] salt)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        }
    }
}
