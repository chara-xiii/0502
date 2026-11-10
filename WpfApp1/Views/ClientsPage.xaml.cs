using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ClientsPage : UserControl
    {
        public ClientsPage()
        {
            InitializeComponent();
            DeleteButton.Visibility = AppState.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                var list = ClientsRepository.GetAll(SearchBox.Text.Trim());
                ClientsGrid.ItemsSource = list;
                FooterText.Text = "Всего клиентов: " + list.Count;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить клиентов: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Client Selected
        {
            get { return ClientsGrid.SelectedItem as Client; }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) LoadData();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            LoadData();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var window = new ClientEditorWindow(null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            var window = new ClientEditorWindow(Selected) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void ClientsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Edit_Click(sender, e);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (Selected == null) return;
            if (MessageBox.Show("Удалить клиента «" + Selected.FullName + "»?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                var error = ClientsRepository.Delete(Selected.ClientId);
                if (error != null)
                {
                    MessageBox.Show(error, "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить клиента: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
