using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class TariffEditorWindow : Window
    {
        private readonly Tariff _editing;

        public TariffEditorWindow(Tariff editing)
        {
            InitializeComponent();
            _editing = editing;

            TypeCombo.ItemsSource = TypesRepository.GetAll();
            if (TypeCombo.Items.Count > 0) TypeCombo.SelectedIndex = 0;

            if (_editing != null)
            {
                TitleText.Text = "Редактирование тарифа";
                var types = TypeCombo.ItemsSource as System.Collections.Generic.List<ShipmentType>;
                TypeCombo.SelectedItem = types.FirstOrDefault(t => t.TypeId == _editing.TypeId);
                WeightBox.Text = _editing.MaxWeight.ToString("0.###", CultureInfo.CurrentCulture);
                PriceBox.Text = _editing.Price.ToString("0.00", CultureInfo.CurrentCulture);
                ActiveCheck.IsChecked = _editing.IsActive;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var type = TypeCombo.SelectedItem as ShipmentType;
            if (type == null)
            {
                ShowError("Выберите тип отправления.");
                return;
            }

            decimal weight;
            decimal price;
            if (!TryParseDecimal(WeightBox.Text, out weight))
            {
                ShowError("Укажите корректный максимальный вес.");
                return;
            }
            if (!TryParseDecimal(PriceBox.Text, out price))
            {
                ShowError("Укажите корректную стоимость.");
                return;
            }

            var error = ValidationService.ValidateTariff(weight, price);
            if (error != null)
            {
                ShowError(error);
                return;
            }

            try
            {
                var tariff = new Tariff
                {
                    TariffId = _editing == null ? 0 : _editing.TariffId,
                    TypeId = type.TypeId,
                    MaxWeight = weight,
                    Price = price,
                    IsActive = ActiveCheck.IsChecked == true
                };

                if (_editing == null) TariffsRepository.Insert(tariff);
                else TariffsRepository.Update(tariff);

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить тариф: " + ex.Message);
            }
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
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
