using System.Windows.Media;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 테마 색상 정보를 제공합니다.
/// </summary>
public sealed class ThemeService
{
    /// <summary>
    /// 테마 이름에 맞는 배경/전경색을 반환합니다.
    /// </summary>
    /// <param name="theme">테마 이름입니다.</param>
    /// <param name="customBackgroundHex">커스텀 배경색입니다.</param>
    /// <param name="customForegroundHex">커스텀 전경색입니다.</param>
    /// <returns>배경색과 전경색입니다.</returns>
    public (Brush Background, Brush Foreground) Resolve(string theme, string customBackgroundHex, string customForegroundHex)
    {
        return theme switch
        {
            "Light" => (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFDFDFD")), new SolidColorBrush(Colors.Black)),
            "Custom" => (new SolidColorBrush((Color)ColorConverter.ConvertFromString(customBackgroundHex)), new SolidColorBrush((Color)ColorConverter.ConvertFromString(customForegroundHex))),
            _ => (new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF121212")), new SolidColorBrush(Colors.WhiteSmoke))
        };
    }
}
