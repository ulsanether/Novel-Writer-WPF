using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NovelWriter.Wpf;

/// <summary>
/// bool 값을 반전합니다. 대상이 <see cref="Visibility"/>면 반전된 값을 Visible/Collapsed로 반환합니다.
/// (예: 진행 중이면 버튼 비활성화, 특정 조건이 아닐 때만 표시)
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    /// <summary>true↔false를 반전합니다. 대상이 Visibility면 Visible/Collapsed로 변환합니다.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var inverted = value is not bool b || !b;
        if (targetType == typeof(Visibility))
        {
            return inverted ? Visibility.Visible : Visibility.Collapsed;
        }

        return inverted;
    }

    /// <summary>true↔false를 반전합니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }

        return value is not bool b || !b;
    }
}
