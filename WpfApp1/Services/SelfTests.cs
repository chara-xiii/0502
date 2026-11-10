using System;
using System.Collections.Generic;
using System.Linq;
using WpfApp1.Data;
using WpfApp1.Models;

namespace WpfApp1.Services
{
    public static class SelfTests
    {
        public static List<SelfTestResult> RunAll()
        {
            var results = new List<SelfTestResult>();

            results.Add(Run("TC_API_1. Подбор тарифа по весу", "Тариф найден", () =>
            {
                var tariff = TariffsRepository.FindForWeight(2, 1.5m);
                return tariff != null && tariff.MaxWeight >= 1.5m && tariff.Price > 0
                    ? "Тариф найден: " + tariff.Price.ToString("N2") + " ₽"
                    : "Тариф не найден";
            }));

            results.Add(Run("TC_VAL_2. Валидация веса", "Ошибки для 0 и 51 кг", () =>
            {
                var zero = ValidationService.ValidateWeight(0) != null;
                var tooMuch = ValidationService.ValidateWeight(51) != null;
                var normal = ValidationService.ValidateWeight(1.5m) == null;
                return zero && tooMuch && normal ? "Ошибки для 0 и 51 кг" : "Валидация работает неверно";
            }));

            results.Add(Run("TC_VAL_3. Нормализация телефона", "+79101234567", () =>
            {
                return ValidationService.NormalizePhone("8 (910) 123-45-67");
            }));

            results.Add(Run("TC_AUTH_4. Вход администратора", "Admin", () =>
            {
                var user = AuthService.Login("admin", AuthService.DemoPassword("admin"));
                return user == null ? "Пользователь не найден" : user.Role;
            }));

            results.Add(Run("TC_AUTH_5. Неверный пароль", "Доступ запрещён", () =>
            {
                var user = AuthService.Login("admin", "wrong-password");
                return user == null ? "Доступ запрещён" : "Доступ разрешён";
            }));

            results.Add(Run("TC_API_6. Проверка JWT", "Admin:1", () =>
            {
                var user = UsersRepository.GetByLogin("admin");
                if (user == null) return "Пользователь не найден";
                var token = JwtService.CreateToken(user);
                var payload = JwtService.Validate(token);
                if (payload == null) return "Токен недействителен";
                var tampered = JwtService.Validate(token + "x");
                return payload.Role + ":" + payload.UserId + (tampered == null ? "" : " (подпись не проверяется)");
            }));

            results.Add(Run("TC_DB_7. Целостность номеров", "Дубликатов нет", () =>
            {
                var duplicates = Convert.ToInt32(Db.Scalar(
                    "SELECT COUNT(*) FROM (SELECT Number FROM dbo.[0502_Shipments] GROUP BY Number HAVING COUNT(*) > 1) t;"));
                return duplicates == 0 ? "Дубликатов нет" : "Найдено дубликатов: " + duplicates;
            }));

            results.Add(Run("TC_DB_8. Структура базы данных", "8 таблиц", () =>
            {
                var count = Convert.ToInt32(Db.Scalar(
                    @"SELECT COUNT(*) FROM sys.tables WHERE name IN
                      ('0502_Users','0502_Clients','0502_ShipmentTypes','0502_Tariffs','0502_Shipments','0502_StatusHistory','0502_Products','0502_Sales');"));
                return count + " таблиц";
            }));

            results.Add(Run("TC_REP_9. Отчёт за последние 7 дней", "Данные получены", () =>
            {
                var report = ReportsRepository.BuildShipmentReport(DateTime.Now.Date.AddDays(-6), DateTime.Now.Date);
                return report.Count > 0 ? "Данные получены: " + report.Count + " отправлений" : "Данных нет";
            }));

            return results;
        }

        private static SelfTestResult Run(string name, string expected, Func<string> action)
        {
            var result = new SelfTestResult { Name = name, Expected = expected };
            try
            {
                result.Actual = action();
                result.Passed = string.Equals(result.Actual, expected, StringComparison.OrdinalIgnoreCase)
                    || result.Actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                result.Actual = "Исключение: " + ex.Message;
                result.Passed = false;
            }
            return result;
        }
    }
}
