using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfApp1.Converters;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class SaleWindow : Window
    {
        private readonly Product _product;
        private readonly ImagePathConverter _imageConverter = new ImagePathConverter();
        private List<Client> _clients;

        public SaleWindow(Product product)
        {
            InitializeComponent();
            _product = product;

            ProductImage.Source = (ImageSource)_imageConverter.Convert(product.ImageFile, typeof(ImageSource), null, null);
            CategoryText.Text = product.Category.ToUpper();
            NameText.Text = product.Name;
            PriceText.Text = product.PriceText;
            DescriptionText.Text = product.Description;
            StockText.Text = "Доступно на складе: " + product.Stock + " шт.";
            EmployeeText.Text = "Оператор: " + AppState.CurrentUserName;

            _clients = ClientsRepository.GetAll();
            _clients.Insert(0, new Client { ClientId = 0, FullName = "Розничная продажа (без клиента)" });
            ClientCombo.ItemsSource = _clients;
            ClientCombo.SelectedIndex = 0;

            QuantityBox.Text = "1";
            UpdateTotal();
            SellButton.IsEnabled = product.Stock > 0;
        }

        private int Quantity
        {
            get
            {
                int value;
                return int.TryParse(QuantityBox.Text, out value) ? value : 0;
            }
        }

        private void QuantityBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateTotal();
        }

        private void UpdateTotal()
        {
            if (_product == null || TotalText == null) return;

            var quantity = Quantity;
            var total = quantity > 0 ? _product.Price * quantity : 0;
            TotalText.Text = total.ToString("N2") + " ₽";
            ErrorText.Visibility = Visibility.Collapsed;
        }

        private void Minus_Click(object sender, RoutedEventArgs e)
        {
            if (Quantity > 1) QuantityBox.Text = (Quantity - 1).ToString();
        }

        private void Plus_Click(object sender, RoutedEventArgs e)
        {
            if (Quantity < _product.Stock) QuantityBox.Text = (Quantity + 1).ToString();
        }

        private void Sell_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var quantity = Quantity;
            if (quantity <= 0)
            {
                ShowError("Количество должно быть больше нуля.");
                return;
            }
            if (quantity > _product.Stock)
            {
                ShowError("На складе недостаточно товара. Доступно: " + _product.Stock + " шт.");
                return;
            }

            var client = ClientCombo.SelectedItem as Client;
            var clientId = client == null || client.ClientId == 0 ? (int?)null : client.ClientId;

            try
            {
                var total = ProductsRepository.Sell(_product.ProductId, clientId, quantity, AppState.CurrentUser.UserId);
                MessageBox.Show("Продажа оформлена.\n" + _product.Name + " × " + quantity + " = " + total.ToString("N2") + " ₽",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
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
