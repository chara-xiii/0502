using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfApp1.Data;
using WpfApp1.Models;

namespace WpfApp1.Views
{
    public partial class ReportsPage : UserControl
    {
        private ReportResult _shipmentReport;
        private ReportResult _salesReport;

        public ReportsPage()
        {
            InitializeComponent();
            FromDate.SelectedDate = DateTime.Today.AddDays(-30);
            ToDate.SelectedDate = DateTime.Today;
            Loaded += (s, e) => BuildReports();
        }

        private void BuildReports()
        {
            if (!FromDate.SelectedDate.HasValue || !ToDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Выберите начало и конец периода.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (FromDate.SelectedDate.Value.Date > ToDate.SelectedDate.Value.Date)
            {
                MessageBox.Show("Начало периода не может быть позже конца.", "Проверка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var from = FromDate.SelectedDate.Value.Date;
                var to = ToDate.SelectedDate.Value.Date;

                _shipmentReport = ReportsRepository.BuildShipmentReport(from, to);
                ShipmentReportGrid.ItemsSource = _shipmentReport.Rows;
                ShipmentChart.ItemsSource = LastDays(_shipmentReport.Days);
                ShipCountCard.Value = _shipmentReport.Count.ToString();
                ShipTotalCard.Value = _shipmentReport.Total.ToString("N0") + " ₽";
                ShipAvgCard.Value = (_shipmentReport.Count == 0 ? 0 : _shipmentReport.Total / _shipmentReport.Count).ToString("N0") + " ₽";

                _salesReport = ReportsRepository.BuildSalesReport(from, to);
                SalesReportGrid.ItemsSource = _salesReport.Rows;
                SalesChart.ItemsSource = LastDays(_salesReport.Days);
                SaleCountCard.Value = _salesReport.Count.ToString();
                SaleTotalCard.Value = _salesReport.Total.ToString("N0") + " ₽";
                SaleAvgCard.Value = (_salesReport.Count == 0 ? 0 : _salesReport.Total / _salesReport.Count).ToString("N0") + " ₽";

                PeriodText.Text = string.Format("Период: {0:dd.MM.yyyy} — {1:dd.MM.yyyy}", from, to);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось построить отчёт: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static System.Collections.Generic.List<DayStat> LastDays(System.Collections.Generic.List<DayStat> days)
        {
            if (days == null) return null;
            return days.Skip(Math.Max(0, days.Count - 14)).ToList();
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            BuildReports();
        }

        private void ReportTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            var isShipments = ReportTabs.SelectedIndex == 0;
            var report = isShipments ? _shipmentReport : _salesReport;
            if (report == null || report.Rows.Count == 0)
            {
                MessageBox.Show("Нет данных для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV-файл (*.csv)|*.csv",
                FileName = (isShipments ? "Отчёт_отправления_" : "Отчёт_продажи_") + DateTime.Now.ToString("yyyyMMdd") + ".csv",
                Title = "Экспорт отчёта"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(isShipments ? "Тип отправления;Количество;Сумма" : "Товар;Количество;Сумма");
                foreach (var row in report.Rows)
                    sb.AppendLine(row.Group + ";" + row.Count + ";" + row.Total.ToString("0.00"));
                sb.AppendLine();
                sb.AppendLine("Итого;" + report.Count + ";" + report.Total.ToString("0.00"));
                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
                MessageBox.Show("Отчёт сохранён.", "Экспорт завершён", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить отчёт: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
