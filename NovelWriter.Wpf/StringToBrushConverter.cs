using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace NovelWriter.Wpf;

/// <summary>
/// ARGB hex 문자열을 <see cref="SolidColorBrush"/>로 변환합니다. (색 팔레트 스와치용)
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    /// <summary>
    /// hex 문자열을 브러시로 변환합니다.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
        }
        catch
        {
            // 무시하고 투명 반환
        }

        return Brushes.Transparent;
    }

    /// <summary>
    /// 지원하지 않습니다.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
