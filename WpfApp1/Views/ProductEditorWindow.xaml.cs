using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfApp1.Converters;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ProductEditorWindow : Window
    {
        private readonly Product _editing;
        private string _imageFile;
        private readonly ImagePathConverter _imageConverter = new ImagePathConverter();

        public ProductEditorWindow(Product editing)
        {
            InitializeComponent();
            _editing = editing;

            CategoryCombo.Items.Add("Конверты");
            CategoryCombo.Items.Add("Марки");
            CategoryCombo.Items.Add("Открытки");
            CategoryCombo.Items.Add("Упаковка");
            CategoryCombo.Items.Add("Сувениры");

            if (_editing != null)
            {
                TitleText.Text = "Редактирование товара";
                NameBox.Text = _editing.Name;
                CategoryCombo.Text = _editing.Category;
                PriceBox.Text = _editing.Price.ToString("0.00", CultureInfo.CurrentCulture);
                StockBox.Text = _editing.Stock.ToString();
                DescriptionBox.Text = _editing.Description;
                ActiveCheck.IsChecked = _editing.IsActive;
                _imageFile = _editing.ImageFile;
            }

            UpdatePreview();
        }

        private void ChooseImage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
                Title = "Выбор изображения товара"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var extension = Path.GetExtension(dialog.FileName);
                var fileName = "product_" + Guid.NewGuid().ToString("N").Substring(0, 8) + extension;
                var targetFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");
                Directory.CreateDirectory(targetFolder);
                File.Copy(dialog.FileName, Path.Combine(targetFolder, fileName), true);
                _imageFile = fileName;
                UpdatePreview();
            }
            catch (Exception ex)
            {
                ShowError("Не удалось скопировать изображение: " + ex.Message);
            }
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrWhiteSpace(_imageFile))
            {
                PreviewImage.Source = null;
                ImageNameText.Text = "Изображение не выбрано";
                return;
            }

            var path = Path.IsPathRooted(_imageFile)
                ? _imageFile
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", _imageFile);
            if (File.Exists(path))
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                PreviewImage.Source = image;
                ImageNameText.Text = Path.GetFileName(path);
            }
            else
            {
                PreviewImage.Source = null;
                ImageNameText.Text = "Файл не найден: " + _imageFile;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var name = NameBox.Text.Trim();
            var category = CategoryCombo.Text.Trim();
            decimal price;
            int stock;

            if (string.IsNullOrWhiteSpace(category))
            {
                ShowError("Укажите категорию товара.");
                return;
            }
            if (!TryParseDecimal(PriceBox.Text, out price))
            {
                ShowError("Укажите корректную цену.");
                return;
            }
            if (!int.TryParse(StockBox.Text.Trim(), out stock))
            {
                ShowError("Укажите корректное количество на складе.");
                return;
            }

            var error = ValidationService.ValidateProduct(name, price, stock);
            if (error != null)
            {
                ShowError(error);
                return;
            }

            try
            {
                var product = new Product
                {
                    ProductId = _editing == null ? 0 : _editing.ProductId,
                    Name = name,
                    Category = category,
                    Price = price,
                    Stock = stock,
                    Description = DescriptionBox.Text.Trim(),
                    ImageFile = _imageFile,
                    IsActive = ActiveCheck.IsChecked == true
                };

                if (_editing == null) ProductsRepository.Insert(product);
                else ProductsRepository.Update(product);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить товар: " + ex.Message);
            }
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
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
