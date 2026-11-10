using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp1.Services;

namespace WpfApp1.Views
{
    public partial class TestsPage : UserControl
    {
        public TestsPage()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (ResultsGrid.ItemsSource == null) RunTests();
            };
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            RunTests();
        }

        private void RunTests()
        {
            try
            {
                RunButton.IsEnabled = false;
                var results = SelfTests.RunAll();
                ResultsGrid.ItemsSource = results;

                var passed = results.Count(r => r.Passed);
                SummaryText.Text = string.Format("Пройдено {0} из {1}", passed, results.Count);
                SummaryBadge.Background = passed == results.Count
                    ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 247, 240))
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(253, 235, 237));
                SummaryText.Foreground = passed == results.Count
                    ? (System.Windows.Media.Brush)FindResource("SuccessBrush")
                    : (System.Windows.Media.Brush)FindResource("AccentBrush");
                FooterText.Text = "Последний запуск: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось выполнить тесты: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }
    }
}
