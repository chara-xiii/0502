using System;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class LoginWindow : Window
    {
        public User User { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => LoginBox.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        private void TryLogin()
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var login = LoginBox.Text.Trim();
            var password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите логин и пароль.");
                return;
            }

            var user = AuthService.Login(login, password);
            if (user == null)
            {
                ShowError("Неверный логин или пароль, либо учётная запись заблокирована.");
                return;
            }

            User = user;
            DialogResult = true;
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Demo_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null) return;
            if (Convert.ToString(button.Tag) == "admin")
            {
                LoginBox.Text = "admin";
                PasswordBox.Password = AuthService.DemoPassword("admin");
            }
            else
            {
                LoginBox.Text = "operator";
                PasswordBox.Password = AuthService.DemoPassword("operator");
            }
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void LeftPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter) TryLogin();
            if (e.Key == Key.Escape) DialogResult = false;
        }
    }
}
