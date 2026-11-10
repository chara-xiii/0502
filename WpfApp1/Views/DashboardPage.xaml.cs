using System;
using System.Windows;
using System.Windows.Controls;
using WpfApp1.Data;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class DashboardPage : UserControl
    {
        public event Action<string> NavigateRequested;

        public DashboardPage()
        {
            InitializeComponent();
            Loaded += DashboardPage_Loaded;
        }

        private void DashboardPage_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshData();
        }

        public void RefreshData()
        {
            try
            {
                var total = ShipmentsRepository.CountTotal();
                var today = ShipmentsRepository.CountToday();
                var revenue = ReportsRepository.RevenueThisMonth();
                var stock = ProductsRepository.TotalStock();

                CardTotal.Value = total.ToString();
                CardToday.Value = today.ToString();
                CardRevenue.Value = revenue.ToString("N0") + " ₽";
                CardStock.Value = stock.ToString();

                var recent = ShipmentsRepository.GetRecent(7);
                RecentGrid.ItemsSource = recent;
                RecentFooter.Text = string.Format("Показано {0} последних записей · всего в системе {1}", recent.Count, total);

                WeekChart.ItemsSource = ReportsRepository.BuildLastWeekStats();
                TopProducts.ItemsSource = ProductsRepository.GetTop(4);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить сводку: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Shipments_Click(object sender, RoutedEventArgs e)
        {
            if (NavigateRequested != null) NavigateRequested("shipments");
        }

        private void Products_Click(object sender, RoutedEventArgs e)
        {
            if (NavigateRequested != null) NavigateRequested("products");
        }

        private void RecentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var item = RecentGrid.SelectedItem as Models.ShipmentView;
            if (item == null || NavigateRequested == null) return;
            NavigateRequested("shipment:" + item.ShipmentId);
        }
    }
}
