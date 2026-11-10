using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp1.Data;
using WpfApp1.Services;
using WpfApp1.Views;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private bool _suppressNavigation;
        private readonly DispatcherTimer _apiTimer;

        public MainWindow()
        {
            InitializeComponent();

            UpdateUserPanel();
            SetDate();
            NavUsers.Visibility = AppState.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            Navigate("dashboard");
            UpdateApiBadge();

            _apiTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _apiTimer.Tick += (s, e) => UpdateApiBadge();
            _apiTimer.Start();

            Closed += (s, e) => Application.Current.Shutdown();
        }

        public void Navigate(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var focusShipmentId = 0;
            if (key.StartsWith("shipment:", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(key.Substring("shipment:".Length), out focusShipmentId);
                key = "shipments";
            }

            if (key == "users" && !AppState.IsAdmin) key = "dashboard";

            SetNavigationChecked(key);

            FrameworkElement page;
            switch (key)
            {
                case "shipments":
                    var shipmentsPage = new ShipmentsPage(focusShipmentId > 0 ? (int?)focusShipmentId : null);
                    shipmentsPage.NavigateRequested += Navigate;
                    page = shipmentsPage;
                    SetHeader("Отправления", "Реестр почтовых отправлений и работа со статусами");
                    break;
                case "products":
                    page = new ProductsPage();
                    SetHeader("Почтовые товары", "Каталог сопутствующих товаров и продажи в отделении");
                    break;
                case "clients":
                    page = new ClientsPage();
                    SetHeader("Клиенты", "Справочник отправителей и получателей");
                    break;
                case "tariffs":
                    page = new TariffsPage();
                    SetHeader("Тарифы", "Действующие тарифы на почтовые отправления");
                    break;
                case "reports":
                    page = new ReportsPage();
                    SetHeader("Отчёты", "Аналитика по отправлениям и продажам товаров");
                    break;
                case "users":
                    page = new UsersPage();
                    SetHeader("Сотрудники", "Учётные записи и права доступа");
                    break;
                case "api":
                    page = new ApiPage();
                    SetHeader("REST API", "Программный интерфейс интеграции с внешними приложениями");
                    break;
                case "tests":
                    page = new TestsPage();
                    SetHeader("Тестирование", "Автоматические проверки основных функций системы");
                    break;
                default:
                    var dashboard = new DashboardPage();
                    dashboard.NavigateRequested += Navigate;
                    page = dashboard;
                    SetHeader("Главная", "Обзор работы отделения");
                    break;
            }

            ContentHost.Content = page;
        }

        private void SetHeader(string title, string subtitle)
        {
            PageTitle.Text = title;
            PageSubtitle.Text = subtitle;
            Title = "Почтовое отделение — " + title;
        }

        private void SetNavigationChecked(string key)
        {
            _suppressNavigation = true;
            try
            {
                NavDashboard.IsChecked = key == "dashboard";
                NavShipments.IsChecked = key == "shipments";
                NavProducts.IsChecked = key == "products";
                NavClients.IsChecked = key == "clients";
                NavTariffs.IsChecked = key == "tariffs";
                NavReports.IsChecked = key == "reports";
                NavUsers.IsChecked = key == "users";
                NavApi.IsChecked = key == "api";
                NavTests.IsChecked = key == "tests";
            }
            finally
            {
                _suppressNavigation = false;
            }
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressNavigation || !IsLoaded) return;
            var radio = sender as RadioButton;
            if (radio == null) return;
            Navigate(Convert.ToString(radio.Uid));
        }

        private void UpdateUserPanel()
        {
            var user = AppState.CurrentUser;
            if (user == null) return;

            UserNameText.Text = user.FullName;
            UserRoleText.Text = user.RoleDisplay + " · " + user.Position;
            var parts = user.FullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            AvatarText.Text = string.Join("", parts.Take(2).Select(p => p.Substring(0, 1))).ToUpper();
        }

        private void SetDate()
        {
            var culture = new CultureInfo("ru-RU");
            var text = DateTime.Now.ToString("dddd, d MMMM yyyy", culture);
            DateText.Text = char.ToUpper(text[0], culture) + text.Substring(1);
        }

        private void UpdateApiBadge()
        {
            if (ApiServer.IsRunning)
            {
                ApiBadge.Background = new SolidColorBrush(Color.FromRgb(231, 247, 240));
                ApiDot.Fill = (Brush)FindResource("SuccessBrush");
                ApiText.Foreground = new SolidColorBrush(Color.FromRgb(30, 122, 85));
                ApiText.Text = "API онлайн · " + ApiServer.Port;
            }
            else
            {
                ApiBadge.Background = new SolidColorBrush(Color.FromRgb(253, 235, 237));
                ApiDot.Fill = (Brush)FindResource("AccentBrush");
                ApiText.Foreground = new SolidColorBrush(Color.FromRgb(180, 40, 52));
                ApiText.Text = "API остановлен";
            }
        }

        private void ApiBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Navigate("api");
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Выйти из учётной записи?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            Hide();
            var login = new LoginWindow();
            if (login.ShowDialog() == true)
            {
                AppState.CurrentUser = login.User;
                UpdateUserPanel();
                NavUsers.Visibility = AppState.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                Navigate("dashboard");
                Show();
                Activate();
            }
            else
            {
                Application.Current.Shutdown();
            }
        }
    }
}
