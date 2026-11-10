using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class TariffsPage : UserControl
    {
        public TariffsPage()
        {
            InitializeComponent();
            var admin = AppState.IsAdmin;
            AddButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            ToggleButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            DeleteButton.Visibility = admin ? Visibility.Visible : Visibility.Collapsed;
            OperatorHint.Visibility = admin ? Visibility.Collapsed : Visibility.Visible;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                var list = TariffsRepository.GetAll();
                TariffsGrid.ItemsSource = list;
                FooterText.Text = string.Format("Тарифов: {0} · действующих: {1}", list.Count, list.Count(t => t.IsActive));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить тарифы: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Tariff Selected
        {
            get { return TariffsGrid.SelectedItem as Tariff; }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsAdmin) return;
            var window = new TariffEditorWindow(null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsAdmin || Selected == null) return;
            var window = new TariffEditorWindow(Selected) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void TariffsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Edit_Click(sender, e);
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsAdmin || Selected == null) return;
            try
            {
                TariffsRepository.Toggle(Selected.TariffId, !Selected.IsActive);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить состояние тарифа: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsAdmin || Selected == null) return;
            if (MessageBox.Show("Удалить тариф «" + Selected.TypeName + " до " + Selected.MaxWeightText + "»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                var error = TariffsRepository.Delete(Selected.TariffId);
                if (error != null)
                {
                    MessageBox.Show(error, "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить тариф: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
