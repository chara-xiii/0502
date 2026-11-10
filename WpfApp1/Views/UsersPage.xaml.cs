using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class UsersPage : UserControl
    {
        public UsersPage()
        {
            InitializeComponent();
            if (!AppState.IsAdmin) IsEnabled = false;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                var list = UsersRepository.GetAll();
                UsersGrid.ItemsSource = list;
                FooterText.Text = "Сотрудников: " + list.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить сотрудников: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private User Selected
        {
            get { return UsersGrid.SelectedItem as User; }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var window = new UserEditorWindow(null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var window = new UserEditorWindow(Selected) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void UsersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Edit_Click(sender, e);
        }

        private void Block_Click(object sender, RoutedEventArgs e)
        {
            var user = Selected;
            if (user == null) return;
            if (user.UserId == AppState.CurrentUser.UserId)
            {
                MessageBox.Show("Нельзя заблокировать собственную учётную запись.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var action = user.IsActive ? "заблокировать" : "разблокировать";
            if (MessageBox.Show("Вы действительно хотите " + action + " сотрудника «" + user.FullName + "»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                UsersRepository.SetActive(user.UserId, !user.IsActive);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить состояние учётной записи: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
