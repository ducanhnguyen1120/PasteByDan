using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using PasteByDan.Models;

namespace PasteByDan.Converters
{
    public class TypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipType t)
            {
                return t switch
                {
                    ClipType.Text => new SolidColorBrush(Color.FromRgb(0x5B, 0x8A, 0xF5)),   // blue
                    ClipType.Link => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),   // green
                    ClipType.Image => new SolidColorBrush(Color.FromRgb(0xAB, 0x47, 0xBC)), // purple
                    ClipType.Code => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),   // orange
                    ClipType.Email => new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),  // cyan
                    ClipType.Phone => new SolidColorBrush(Color.FromRgb(0xFF, 0xD6, 0x00)),  // yellow
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class TypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipType t)
                return t switch
                {
                    ClipType.Text => "TXT",
                    ClipType.Link => "URL",
                    ClipType.Image => "IMG",
                    ClipType.Code => "CODE",
                    ClipType.Email => "EMAIL",
                    ClipType.Phone => "PHONE",
                    _ => "?"
                };
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool bv && bv;
            bool invert = parameter as string == "invert";
            b = invert ? !b : b;
            return b ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value == null ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class Base64ToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string b64 && !string.IsNullOrEmpty(b64))
                return Services.ClipboardService.Base64ToBitmapSource(b64);
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PinIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "📌" : "📍";
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
