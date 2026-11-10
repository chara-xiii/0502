using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp1.Converters;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ShipmentEditorWindow : Window
    {
        private readonly ShipmentView _editing;
        private List<Client> _clients;
        private readonly ImagePathConverter _imageConverter = new ImagePathConverter();
        private Tariff _currentTariff;

        public ShipmentEditorWindow(ShipmentView editing)
        {
            InitializeComponent();
            _editing = editing;

            TypeCombo.ItemsSource = TypesRepository.GetAll();
            _clients = ClientsRepository.GetAll();
            SenderCombo.ItemsSource = _clients;
            RecipientCombo.ItemsSource = _clients;

            if (_editing == null)
            {
                DateBox.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                WeightBox.Text = "0,100";
                if (TypeCombo.Items.Count > 0) TypeCombo.SelectedIndex = 0;
                if (_clients.Count > 0) SenderCombo.SelectedIndex = 0;
                if (_clients.Count > 1) RecipientCombo.SelectedIndex = 1;
                StatusHint.Text = "После сохранения отправлению присваивается уникальный номер и статус «Принято».";
            }
            else
            {
                TitleText.Text = "Редактирование отправления";
                SubtitleText.Text = "Изменение параметров отправления " + _editing.Number;
                SaveButton.Content = "Сохранить изменения";
                NumberText.Text = "Номер: " + _editing.Number;
                DateBox.Text = _editing.CreatedAtText;
                CommentBox.Text = _editing.Comment;
                WeightBox.Text = _editing.Weight.ToString("0.###", CultureInfo.CurrentCulture);
                StatusHint.Text = "Изменение параметров не меняет уже сохранённую историю статусов.";

                var type = (TypeCombo.ItemsSource as List<ShipmentType>).FirstOrDefault(t => t.TypeId == _editing.TypeId);
                TypeCombo.SelectedItem = type;
                SenderCombo.SelectedItem = _clients.FirstOrDefault(c => c.ClientId == _editing.SenderId);
                RecipientCombo.SelectedItem = _clients.FirstOrDefault(c => c.ClientId == _editing.RecipientId);
            }
        }

        private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void WeightBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Recalculate();
        }

        private void UpdatePreview()
        {
            var type = TypeCombo.SelectedItem as ShipmentType;
            if (type == null) return;
            TypeImage.Source = (ImageSource)_imageConverter.Convert(type.ImageFile, typeof(ImageSource), null, null);
            TypeNameText.Text = type.Name;
            TypeDescriptionText.Text = type.Description;
            Recalculate();
        }

        private void Recalculate()
        {
            var type = TypeCombo.SelectedItem as ShipmentType;
            if (type == null) return;

            decimal weight;
            if (!TryParseDecimal(WeightBox.Text, out weight) || weight <= 0)
            {
                _currentTariff = null;
                TariffText.Text = "Укажите корректный вес";
                CostText.Text = "0,00 ₽";
                return;
            }

            _currentTariff = TariffsRepository.FindForWeight(type.TypeId, weight);
            if (_currentTariff == null)
            {
                TariffText.Text = "Нет действующего тарифа для веса " + weight.ToString("0.###") + " кг";
                CostText.Text = "0,00 ₽";
                return;
            }

            TariffText.Text = string.Format("Тариф до {0} кг · {1:N2} ₽", _currentTariff.MaxWeight.ToString("0.###"), _currentTariff.Price);
            CostText.Text = _currentTariff.Price.ToString("N2") + " ₽";
        }

        private static bool TryParseDecimal(string text, out decimal value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var normalized = text.Trim().Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private void AddClient_Click(object sender, RoutedEventArgs e)
        {
            var window = new ClientEditorWindow(null) { Owner = this };
            if (window.ShowDialog() == true && window.SavedClient != null)
            {
                _clients = ClientsRepository.GetAll();
                SenderCombo.ItemsSource = _clients;
                RecipientCombo.ItemsSource = _clients;
                var button = sender as Button;
                var target = button == null ? "" : Convert.ToString(button.Tag);
                if (target == "sender")
                    SenderCombo.SelectedItem = _clients.FirstOrDefault(c => c.ClientId == window.SavedClient.ClientId);
                else
                    RecipientCombo.SelectedItem = _clients.FirstOrDefault(c => c.ClientId == window.SavedClient.ClientId);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var type = TypeCombo.SelectedItem as ShipmentType;
            var senderClient = SenderCombo.SelectedItem as Client;
            var recipientClient = RecipientCombo.SelectedItem as Client;

            if (type == null) { ShowError("Выберите тип отправления."); return; }
            if (senderClient == null) { ShowError("Выберите отправителя."); return; }
            if (recipientClient == null) { ShowError("Выберите получателя."); return; }
            if (senderClient.ClientId == recipientClient.ClientId) { ShowError("Отправитель и получатель должны различаться."); return; }

            decimal weight;
            if (!TryParseDecimal(WeightBox.Text, out weight)) { ShowError("Укажите корректный вес в килограммах."); return; }
            var weightError = ValidationService.ValidateWeight(weight);
            if (weightError != null) { ShowError(weightError); return; }

            var tariff = TariffsRepository.FindForWeight(type.TypeId, weight);
            if (tariff == null)
            {
                ShowError("Для указанного веса нет действующего тарифа. Измените вес или обратитесь к администратору.");
                return;
            }

            try
            {
                if (_editing == null)
                {
                    var shipment = new Shipment
                    {
                        TypeId = type.TypeId,
                        SenderId = senderClient.ClientId,
                        RecipientId = recipientClient.ClientId,
                        TariffId = tariff.TariffId,
                        Weight = weight,
                        Cost = tariff.Price,
                        EmployeeId = AppState.CurrentUser.UserId,
                        Comment = CommentBox.Text
                    };
                    var number = ShipmentsRepository.Create(shipment, CommentBox.Text);
                    MessageBox.Show("Отправление " + number + " зарегистрировано.\nСтоимость: " + tariff.Price.ToString("N2") + " ₽",
                        "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var shipment = new Shipment
                    {
                        ShipmentId = _editing.ShipmentId,
                        TypeId = type.TypeId,
                        SenderId = senderClient.ClientId,
                        RecipientId = recipientClient.ClientId,
                        TariffId = tariff.TariffId,
                        Weight = weight,
                        Cost = tariff.Price,
                        Comment = CommentBox.Text
                    };
                    ShipmentsRepository.Update(shipment);
                }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ShowError("Не удалось сохранить отправление: " + ex.Message);
            }
        }

        private void ShowError(string text)
        {
            ErrorText.Text = text;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
