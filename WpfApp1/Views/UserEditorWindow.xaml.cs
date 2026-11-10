using System;
using System.Windows;
using System.Windows.Controls;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class UserEditorWindow : Window
    {
        private readonly User _editing;

        public UserEditorWindow(User editing)
        {
            InitializeComponent();
            _editing = editing;
            RoleCombo.SelectedIndex = 0;

            if (_editing != null)
            {
                TitleText.Text = "Редактирование сотрудника";
                FullNameBox.Text = _editing.FullName;
                PositionBox.Text = _editing.Position;
                PhoneBox.Text = _editing.Phone;
                LoginBox.Text = _editing.Login;
                ActiveCheck.IsChecked = _editing.IsActive;
                RoleCombo.SelectedIndex = _editing.Role == "Admin" ? 1 : 0;
                PasswordLabel.Text = "НОВЫЙ ПАРОЛЬ (НЕОБЯЗАТЕЛЬНО)";
                PasswordHint.Text = "Оставьте пустым, чтобы сохранить текущий пароль";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var fullName = FullNameBox.Text.Trim();
            var position = PositionBox.Text.Trim();
            var phone = ValidationService.NormalizePhone(PhoneBox.Text);
            var login = LoginBox.Text.Trim();
            var password = PasswordBoxControl.Password;
            var roleItem = RoleCombo.SelectedItem as ComboBoxItem;
            var role = roleItem == null ? "Operator" : Convert.ToString(roleItem.Tag);

            var error = ValidationService.ValidateUser(fullName, position, login, password, _editing == null);
            if (error != null)
            {
                ShowError(error);
                return;
            }
            if (!string.IsNullOrWhiteSpace(phone) && !ValidationService.IsPhoneValid(phone))
            {
                ShowError("Телефон должен быть в формате +7XXXXXXXXXX.");
                return;
            }
            if (UsersRepository.LoginExists(login, _editing == null ? 0 : _editing.UserId))
            {
                ShowError("Пользователь с таким логином уже существует.");
                return;
            }
            if (_editing != null && _editing.UserId == AppState.CurrentUser.UserId && role != "Admin")
            {
                ShowError("Нельзя снять с себя роль администратора.");
                return;
            }

            try
            {
                var user = new User
                {
                    UserId = _editing == null ? 0 : _editing.UserId,
                    FullName = fullName,
                    Position = position,
                    Phone = phone,
                    Login = login,
                    Role = role,
                    IsActive = ActiveCheck.IsChecked == true
                };

                if (_editing == null) UsersRepository.Insert(user, password);
                else UsersRepository.Update(user, password);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить сотрудника: " + ex.Message);
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
