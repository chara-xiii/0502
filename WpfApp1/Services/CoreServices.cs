using System;
using System.Text.RegularExpressions;
using WpfApp1.Data;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public static class AppState
    {
        public static User CurrentUser { get; set; }

        public static bool IsAdmin
        {
            get { return CurrentUser != null && CurrentUser.Role == "Admin"; }
        }

        public static string CurrentUserName
        {
            get { return CurrentUser == null ? "" : CurrentUser.FullName; }
        }

        public static string CurrentUserRole
        {
            get { return CurrentUser == null ? "" : CurrentUser.RoleDisplay; }
        }
    }

    public static class AuthService
    {
        internal static string DemoPassword(string login)
        {
            var name = login == "admin" ? "POSTOFFICE_DEMO_ADMIN_PASSWORD" : "POSTOFFICE_DEMO_OPERATOR_PASSWORD";
            return Environment.GetEnvironmentVariable(name) ?? "";
        }

        public static User Login(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                return null;

            var users = UsersRepository.GetByLogin(login.Trim());
            if (users == null || !users.IsActive) return null;
            if (!PasswordHasher.Verify(password, users.PasswordHash)) return null;
            return users;
        }
    }

    public static class ValidationService
    {
        private static readonly Regex PhoneRegex = new Regex(@"^(\+7|8)\d{10}$", RegexOptions.Compiled);
        private static readonly Regex LoginRegex = new Regex(@"^[A-Za-z0-9_]{4,20}$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "";
            var digits = Regex.Replace(phone, @"[^\d]", "");
            if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits.Substring(1);
            if (digits.Length == 10) digits = "7" + digits;
            if (digits.Length == 11 && digits[0] == '7') return "+" + digits;
            return phone.Trim();
        }

        public static bool IsPhoneValid(string phone)
        {
            return PhoneRegex.IsMatch(NormalizePhone(phone));
        }

        public static string ValidateClient(string fullName, string phone, string address, string email)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 5)
                return "Укажите ФИО клиента (не менее 5 символов).";
            if (!IsPhoneValid(phone))
                return "Телефон должен быть в формате +7XXXXXXXXXX (11 цифр).";
            if (string.IsNullOrWhiteSpace(address) || address.Trim().Length < 5)
                return "Укажите адрес клиента.";
            if (!string.IsNullOrWhiteSpace(email) && !EmailRegex.IsMatch(email.Trim()))
                return "Укажите корректный адрес электронной почты.";
            return null;
        }

        public static string ValidateWeight(decimal weight)
        {
            if (weight <= 0) return "Вес должен быть больше нуля.";
            if (weight > 50) return "Вес одного отправления не может превышать 50 кг.";
            return null;
        }

        public static string ValidateUser(string fullName, string position, string login, string password, bool isNew)
        {
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 5)
                return "Укажите ФИО сотрудника.";
            if (string.IsNullOrWhiteSpace(position))
                return "Укажите должность сотрудника.";
            if (!LoginRegex.IsMatch(login ?? ""))
                return "Логин: 4–20 символов, только латинские буквы, цифры и подчёркивание.";
            if (isNew && (password == null || password.Length < 6))
                return "Пароль должен содержать не менее 6 символов.";
            if (!isNew && !string.IsNullOrEmpty(password) && password.Length < 6)
                return "Новый пароль должен содержать не менее 6 символов.";
            return null;
        }

        public static string ValidateTariff(decimal maxWeight, decimal price)
        {
            if (maxWeight <= 0) return "Максимальный вес должен быть больше нуля.";
            if (maxWeight > 50) return "Максимальный вес не может превышать 50 кг.";
            if (price < 0) return "Стоимость не может быть отрицательной.";
            return null;
        }

        public static string ValidateProduct(string name, decimal price, int stock)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 3)
                return "Укажите наименование товара.";
            if (price <= 0) return "Цена товара должна быть больше нуля.";
            if (stock < 0) return "Количество на складе не может быть отрицательным.";
            return null;
        }
    }
}
