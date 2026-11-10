using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp1.Converters
{
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var fileName = value as string;
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            string path = fileName;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images", fileName);

            if (!File.Exists(path)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class StatusBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? "";
            switch (status)
            {
                case "Принято": return new SolidColorBrush(Color.FromRgb(22, 86, 165));
                case "В обработке": return new SolidColorBrush(Color.FromRgb(224, 138, 30));
                case "Отправлено": return new SolidColorBrush(Color.FromRgb(107, 78, 190));
                case "Доставлено": return new SolidColorBrush(Color.FromRgb(30, 158, 106));
                case "Возврат": return new SolidColorBrush(Color.FromRgb(230, 57, 70));
                default: return new SolidColorBrush(Color.FromRgb(107, 122, 144));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class StatusBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var status = value as string ?? "";
            switch (status)
            {
                case "Принято": return new SolidColorBrush(Color.FromRgb(232, 240, 251));
                case "В обработке": return new SolidColorBrush(Color.FromRgb(253, 244, 227));
                case "Отправлено": return new SolidColorBrush(Color.FromRgb(240, 236, 251));
                case "Доставлено": return new SolidColorBrush(Color.FromRgb(231, 247, 240));
                case "Возврат": return new SolidColorBrush(Color.FromRgb(253, 235, 237));
                default: return new SolidColorBrush(Color.FromRgb(243, 246, 251));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool && (bool)value;
            if (Invert) flag = !flag;
            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hasValue = value != null;
            if (Invert) hasValue = !hasValue;
            return hasValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class InverseBoolToVisibilityConverter : BoolToVisibilityConverter
    {
        public InverseBoolToVisibilityConverter()
        {
            Invert = true;
        }
    }
}
