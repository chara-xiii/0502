using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Views;

namespace WpfApp1.Services
{
    /// <summary>
    /// Формирует набор иллюстраций интерфейса для пояснительной записки.
    /// Запуск: WpfApp1.exe --screenshots
    /// </summary>
    public static class ScreenshotRunner
    {
        private static string _outputDirectory;

        public static void Run()
        {
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
            Directory.CreateDirectory(_outputDirectory);

            AppState.CurrentUser = UsersRepository.GetByLogin("admin");
            ApiServer.Start();

            CaptureLoginWindow();

            var main = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 30,
                Top = 30,
                Width = 1480,
                Height = 900
            };
            main.Show();
            Wait(900);

            var content = main.Content as FrameworkElement;
            if (content == null) throw new InvalidOperationException("Не удалось получить содержимое главного окна.");

            var pages = new[]
            {
                new { Key = "dashboard", File = "02_dashboard.png" },
                new { Key = "shipments", File = "03_shipments.png" },
                new { Key = "products", File = "04_products.png" },
                new { Key = "clients", File = "05_clients.png" },
                new { Key = "tariffs", File = "06_tariffs.png" },
                new { Key = "reports", File = "07_reports.png" },
                new { Key = "users", File = "08_users.png" },
                new { Key = "api", File = "09_api.png" },
                new { Key = "tests", File = "10_tests.png" }
            };

            foreach (var page in pages)
            {
                main.Navigate(page.Key);
                Wait(750);
                Capture(content, page.File);
            }

            var shipment = ShipmentsRepository.GetRecent(1).FirstOrDefault();
            CaptureEditor(new ShipmentEditorWindow(null), "11_shipment_new.png");

            if (shipment != null)
            {
                CaptureEditor(new ShipmentEditorWindow(shipment), "12_shipment_edit.png");
            }

            var product = ProductsRepository.GetAll().FirstOrDefault();
            if (product != null)
            {
                CaptureEditor(new ProductEditorWindow(product), "13_product_edit.png");
                CaptureEditor(new SaleWindow(product), "14_sale.png");
            }

            var client = ClientsRepository.GetAll().FirstOrDefault();
            if (client != null)
            {
                CaptureEditor(new ClientEditorWindow(client), "15_client_edit.png");
            }

            var user = UsersRepository.GetAll().FirstOrDefault();
            if (user != null)
            {
                CaptureEditor(new UserEditorWindow(user), "16_user_edit.png");
            }

            var tariff = TariffsRepository.GetAll().FirstOrDefault();
            if (tariff != null)
            {
                CaptureEditor(new TariffEditorWindow(tariff), "17_tariff_edit.png");
            }

            main.Hide();
            Wait(200);
            ApiServer.Stop();
        }

        private static void CaptureLoginWindow()
        {
            var login = new LoginWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 80,
                Top = 60
            };
            login.Show();
            Wait(700);

            var loginBox = login.FindName("LoginBox") as TextBox;
            var passwordBox = login.FindName("PasswordBox") as PasswordBox;
            if (loginBox != null) loginBox.Text = "admin";
            if (passwordBox != null) passwordBox.Password = "";

            Wait(250);
            Capture(login.Content as FrameworkElement, "01_login.png");
            login.Close();
            Wait(250);
        }

        private static void CaptureEditor(Window window, string fileName)
        {
            var root = window.Content as FrameworkElement;
            if (root != null) root.Margin = new Thickness(0);

            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Show();
            Wait(650);
            Capture(root, fileName);
            window.Close();
            Wait(150);
        }

        private static void Capture(FrameworkElement element, string fileName)
        {
            if (element == null) return;

            element.UpdateLayout();
            var width = (int)Math.Ceiling(element.ActualWidth);
            var height = (int)Math.Ceiling(element.ActualHeight);
            if (width <= 0 || height <= 0)
            {
                width = (int)Math.Ceiling(element.Width);
                height = (int)Math.Ceiling(element.Height);
            }
            if (width <= 0 || height <= 0) return;

            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            var path = Path.Combine(_outputDirectory, fileName);
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private static void Wait(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }
}
