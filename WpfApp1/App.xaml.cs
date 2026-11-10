using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using WpfApp1.Data;
using WpfApp1.Services;
using WpfApp1.Views;

namespace WpfApp1
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var culture = new CultureInfo("ru-RU");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ru-RU")));

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("Произошла непредвиденная ошибка:\n" + args.Exception.Message,
                    "Ошибка приложения", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            try
            {
                Db.EnsureCreated();
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.txt"), ex.ToString());
                }
                catch { }

                MessageBox.Show(
                    "Не удалось инициализировать базу данных MS SQL Server.\n\n" +
                    ex.Message + "\n\nПроверьте параметры подключения и настройки первого запуска в README.",
                    "Запуск приложения", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            if (e.Args.Any(a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase)))
            {
                RunSelfTest();
                return;
            }

            if (e.Args.Any(a => string.Equals(a, "--screenshots", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    ScreenshotRunner.Run();
                }
                catch (Exception ex)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshots_error.txt"), ex.ToString());
                }
                Shutdown();
                return;
            }

            ApiServer.Start();

            AppState.CurrentUser = null;
            var login = new LoginWindow();
            if (login.ShowDialog() == true)
            {
                AppState.CurrentUser = login.User;
                var mainWindow = new MainWindow();
                MainWindow = mainWindow;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }

        private void RunSelfTest()
        {
            var results = SelfTests.RunAll();
            var sb = new StringBuilder();
            sb.AppendLine("PostOffice self-test: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(new string('-', 90));
            foreach (var result in results)
            {
                sb.AppendLine((result.Passed ? "[PASS] " : "[FAIL] ") + result.Name);
                sb.AppendLine("  Expected: " + result.Expected);
                sb.AppendLine("  Actual:   " + result.Actual);
            }
            sb.AppendLine(new string('-', 90));
            sb.AppendLine("Passed: " + results.Count(r => r.Passed) + " / " + results.Count);

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            Environment.ExitCode = results.All(r => r.Passed) ? 0 : 1;
            Shutdown(Environment.ExitCode);
        }
    }
}
