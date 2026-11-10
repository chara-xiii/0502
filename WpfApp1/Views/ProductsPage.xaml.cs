using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ProductsPage : UserControl
    {
        private bool _initialized;

        public bool CanEdit
        {
            get { return AppState.IsAdmin; }
        }

        public ProductsPage()
        {
            InitializeComponent();
            DataContext = this;

            CategoryCombo.Items.Add("Все категории");
            foreach (var category in ProductsRepository.GetCategories()) CategoryCombo.Items.Add(category);
            CategoryCombo.SelectedIndex = 0;

            AddButton.Visibility = CanEdit ? Visibility.Visible : Visibility.Collapsed;
            _initialized = true;
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                var category = CategoryCombo.SelectedIndex <= 0 ? "" : Convert.ToString(CategoryCombo.SelectedItem);
                var list = ProductsRepository.GetAll(SearchBox.Text.Trim(), category);
                ProductsList.ItemsSource = list;
                EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                FooterText.Text = string.Format("Показано товаров: {0} · единиц на складе: {1}", list.Count, ProductsRepository.TotalStock());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить каталог: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) LoadData();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_initialized) LoadData();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _initialized = false;
            SearchBox.Text = "";
            CategoryCombo.SelectedIndex = 0;
            _initialized = true;
            LoadData();
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var window = new ProductEditorWindow(null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (!CanEdit) return;
            var button = sender as Button;
            var product = button == null ? null : button.Tag as Product;
            if (product == null) return;
            var window = new ProductEditorWindow(product) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }

        private void Sell_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var product = button == null ? null : button.Tag as Product;
            if (product == null) return;
            var window = new SaleWindow(product) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData();
        }
    }
}
