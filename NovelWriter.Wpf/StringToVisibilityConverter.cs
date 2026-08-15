using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NovelWriter.Wpf;

/// <summary>
/// 문자열이 비어 있지 않으면 Visible, 비어 있으면 Collapsed로 변환합니다.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    /// <summary>문자열 유무를 Visibility로 변환합니다.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>지원하지 않습니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
