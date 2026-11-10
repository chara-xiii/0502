using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ApiPage : UserControl
    {
        private readonly ObservableCollection<string> _log = new ObservableCollection<string>();
        private readonly DispatcherTimer _timer;

        public ApiPage()
        {
            InitializeComponent();
            LogList.ItemsSource = _log;
            EndpointsList.ItemsSource = BuildEndpoints();
            TokenBox.Text = JwtService.CreateToken(AppState.CurrentUser);
            TokenHint.Text = "Действует 8 часов";

            ApiServer.RequestLogged += ApiServer_RequestLogged;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();
            UpdateStatus();

            Unloaded += (s, e) =>
            {
                ApiServer.RequestLogged -= ApiServer_RequestLogged;
                _timer.Stop();
            };
        }

        private void ApiServer_RequestLogged(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _log.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + message);
                while (_log.Count > 120) _log.RemoveAt(_log.Count - 1);
                RequestCountText.Text = ApiServer.RequestCount.ToString();
            });
        }

        private void UpdateStatus()
        {
            var running = ApiServer.IsRunning;
            StatusText.Text = running ? "API работает" : "API остановлен";
            StatusText.Foreground = running
                ? new SolidColorBrush(Color.FromRgb(27, 36, 48))
                : new SolidColorBrush(Color.FromRgb(180, 40, 52));
            StatusDot.Fill = running
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("AccentBrush");
            BaseUrlText.Text = ApiServer.BaseUrl + "/swagger";
            RequestCountText.Text = ApiServer.RequestCount.ToString();
            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            var error = ApiServer.Start();
            if (error != null) MessageBox.Show(error, "API", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateStatus();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            ApiServer.Stop();
            UpdateStatus();
        }

        private void OpenSwagger_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("/swagger");
        }

        private void OpenJson_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl("/swagger/v1/swagger.json");
        }

        private void OpenUrl(string path)
        {
            if (!ApiServer.IsRunning)
            {
                MessageBox.Show("Сначала запустите API.", "API", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo(ApiServer.BaseUrl + path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть браузер: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateToken_Click(object sender, RoutedEventArgs e)
        {
            TokenBox.Text = JwtService.CreateToken(AppState.CurrentUser);
            TokenHint.Text = "Новый токен сформирован";
        }

        private void CopyToken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(TokenBox.Text);
                TokenHint.Text = "Скопировано в буфер обмена";
            }
            catch
            {
                TokenHint.Text = "Не удалось скопировать";
            }
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            _log.Clear();
        }

        private static ObservableCollection<EndpointInfo> BuildEndpoints()
        {
            return new ObservableCollection<EndpointInfo>
            {
                new EndpointInfo("POST", "/api/auth/login", "Авторизация и получение JWT", "#E08A1E"),
                new EndpointInfo("GET", "/api/shipments", "Список отправлений с фильтрацией", "#1E9E6A"),
                new EndpointInfo("POST", "/api/shipments", "Регистрация отправления", "#E08A1E"),
                new EndpointInfo("GET", "/api/shipments/{number}", "Карточка и история статусов", "#1E9E6A"),
                new EndpointInfo("PUT", "/api/shipments/{number}/status", "Изменение статуса отправления", "#1656A5"),
                new EndpointInfo("GET", "/api/tariffs", "Действующие тарифы", "#1E9E6A"),
                new EndpointInfo("GET", "/api/products", "Каталог почтовых товаров", "#1E9E6A"),
                new EndpointInfo("POST", "/api/sales", "Продажа товара", "#E08A1E"),
                new EndpointInfo("GET", "/api/reports/shipments", "Отчёт по отправлениям за период", "#1E9E6A"),
                new EndpointInfo("GET", "/api/reports/sales", "Отчёт по продажам за период", "#1E9E6A"),
                new EndpointInfo("GET", "/api/health", "Проверка работоспособности сервера", "#1E9E6A")
            };
        }

        public class EndpointInfo
        {
            public EndpointInfo(string method, string path, string description, string color)
            {
                Method = method;
                Path = path;
                Description = description;
                Color = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }

            public string Method { get; private set; }
            public string Path { get; private set; }
            public string Description { get; private set; }
            public Brush Color { get; private set; }
        }
    }
}
