using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WpfApp1.Converters;
using WpfApp1.Data;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class ShipmentsPage : UserControl
    {
        private bool _initialized;
        private readonly int? _initialShipmentId;
        private readonly ImagePathConverter _imageConverter = new ImagePathConverter();
        private readonly StatusBrushConverter _statusBrush = new StatusBrushConverter();
        private readonly StatusBackgroundConverter _statusBackground = new StatusBackgroundConverter();

        public event Action<string> NavigateRequested;

        public ShipmentsPage(int? initialShipmentId = null)
        {
            InitializeComponent();
            _initialShipmentId = initialShipmentId;

            StatusFilter.Items.Add("Все статусы");
            foreach (var status in ShipmentsRepository.Statuses) StatusFilter.Items.Add(status);
            StatusFilter.SelectedIndex = 0;

            FromDate.SelectedDate = DateTime.Today.AddDays(-30);
            ToDate.SelectedDate = DateTime.Today;
            DeleteButton.Visibility = AppState.IsAdmin ? Visibility.Visible : Visibility.Collapsed;

            _initialized = true;
            Loaded += (s, e) => LoadData(_initialShipmentId);
        }

        private void LoadData(int? selectId)
        {
            try
            {
                var status = StatusFilter.SelectedIndex <= 0 ? "" : Convert.ToString(StatusFilter.SelectedItem);
                var query = SearchBox == null ? "" : SearchBox.Text.Trim();
                var list = ShipmentsRepository.GetAll(query, status, FromDate.SelectedDate, ToDate.SelectedDate);
                ShipmentsGrid.ItemsSource = list;
                FooterText.Text = string.Format("Найдено: {0} · период: {1} — {2}",
                    list.Count,
                    FromDate.SelectedDate.HasValue ? FromDate.SelectedDate.Value.ToString("dd.MM.yyyy") : "начало",
                    ToDate.SelectedDate.HasValue ? ToDate.SelectedDate.Value.ToString("dd.MM.yyyy") : "сегодня");

                ShipmentView selected = null;
                if (selectId.HasValue) selected = list.FirstOrDefault(x => x.ShipmentId == selectId.Value);
                if (selected == null) selected = list.FirstOrDefault();

                ShipmentsGrid.SelectedItem = selected;
                if (selected == null) ShowEmpty();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить отправления: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowEmpty()
        {
            EmptyPanel.Visibility = Visibility.Visible;
            DetailPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDetail(ShipmentView shipment)
        {
            if (shipment == null)
            {
                ShowEmpty();
                return;
            }

            EmptyPanel.Visibility = Visibility.Collapsed;
            DetailPanel.Visibility = Visibility.Visible;

            DetailImage.Source = (ImageSource)_imageConverter.Convert(shipment.TypeImage, typeof(ImageSource), null, null);
            DetailNumber.Text = shipment.Number;
            DetailStatusText.Text = shipment.Status;
            DetailStatusText.Foreground = (Brush)_statusBrush.Convert(shipment.Status, typeof(Brush), null, null);
            DetailStatusBorder.Background = (Brush)_statusBackground.Convert(shipment.Status, typeof(Brush), null, null);
            DetailType.Text = shipment.TypeName;
            DetailSender.Text = shipment.SenderName;
            DetailSenderAddress.Text = shipment.SenderPhone + " · " + shipment.SenderAddress;
            DetailRecipient.Text = shipment.RecipientName;
            DetailRecipientAddress.Text = shipment.RecipientPhone + " · " + shipment.RecipientAddress;
            DetailWeight.Text = shipment.WeightText;
            DetailCost.Text = shipment.CostText;
            DetailDate.Text = shipment.CreatedAtText;
            DetailEmployee.Text = shipment.EmployeeName;
            DetailComment.Text = string.IsNullOrWhiteSpace(shipment.Comment) ? "—" : shipment.Comment;

            StatusCombo.ItemsSource = ShipmentsRepository.Statuses;
            StatusCombo.SelectedItem = shipment.Status;
            StatusComment.Text = "";

            HistoryList.ItemsSource = ShipmentsRepository.GetHistory(shipment.ShipmentId);
        }

        private ShipmentView Selected
        {
            get { return ShipmentsGrid.SelectedItem as ShipmentView; }
        }

        private void ShipmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowDetail(Selected);
        }

        private void ShipmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Selected != null) OpenEditor(Selected);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            LoadData(null);
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) LoadData(null);
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (!_initialized) return;
            LoadData(null);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _initialized = false;
            SearchBox.Text = "";
            StatusFilter.SelectedIndex = 0;
            FromDate.SelectedDate = DateTime.Today.AddDays(-30);
            ToDate.SelectedDate = DateTime.Today;
            _initialized = true;
            LoadData(null);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData(Selected == null ? (int?)null : Selected.ShipmentId);
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            var window = new ShipmentEditorWindow(null) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData(null);
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (Selected != null) OpenEditor(Selected);
        }

        private void OpenEditor(ShipmentView shipment)
        {
            var window = new ShipmentEditorWindow(shipment) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() == true) LoadData(shipment.ShipmentId);
        }

        private void SaveStatus_Click(object sender, RoutedEventArgs e)
        {
            var shipment = Selected;
            if (shipment == null) return;

            var status = Convert.ToString(StatusCombo.SelectedItem);
            if (string.IsNullOrWhiteSpace(status))
            {
                MessageBox.Show("Выберите новый статус.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (status == shipment.Status)
            {
                MessageBox.Show("У отправления уже установлен этот статус.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShipmentsRepository.UpdateStatus(shipment.ShipmentId, status, AppState.CurrentUser.UserId, StatusComment.Text);
                LoadData(shipment.ShipmentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось изменить статус: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var shipment = Selected;
            if (shipment == null) return;
            if (MessageBox.Show("Удалить отправление " + shipment.Number + "? Действие необратимо.",
                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                ShipmentsRepository.Delete(shipment.ShipmentId);
                LoadData(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось удалить отправление: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var list = ShipmentsGrid.ItemsSource as List<ShipmentView>;
            if (list == null || list.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV-файл (*.csv)|*.csv",
                FileName = "Отправления_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".csv",
                Title = "Экспорт реестра отправлений"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Номер;Тип;Отправитель;Получатель;Дата;Вес;Стоимость;Статус");
                foreach (var item in list)
                {
                    sb.AppendLine(string.Join(";", new[]
                    {
                        item.Number, item.TypeName, item.SenderName, item.RecipientName,
                        item.CreatedAtText, item.WeightText, item.CostText, item.Status
                    }));
                }
                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Экспортировано записей: " + list.Count, "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить файл: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
