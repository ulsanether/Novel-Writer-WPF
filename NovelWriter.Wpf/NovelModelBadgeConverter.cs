using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NovelWriter.Wpf;

/// <summary>
/// 모델 이름 기반 배지 판별을 위한 공용 키워드입니다.
/// </summary>
internal static class ModelBadgeKeywords
{
    /// <summary>무검열/검열 해제 모델 키워드입니다.</summary>
    public static readonly string[] Uncensored = { "uncensored", "abliterated", "dolphin", "hermes" };

    /// <summary>소설/창작 특화 모델 키워드입니다.</summary>
    public static readonly string[] Novel = { "magnum", "novel", "story", "writer" };

    /// <summary>한글 특화 모델 키워드입니다.</summary>
    public static readonly string[] Korean = { "exaone", "kanana", "korean", "ko-", "koni", "bllossom" };

    public static bool Matches(string? name, string[] keywords)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var keyword in keywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// 소설/창작 특화(무검열 아님) 모델이면 "노벨 특화" 배지를 표시합니다.
/// </summary>
public sealed class NovelModelBadgeConverter : IValueConverter
{
    /// <summary>노벨 특화(무검열 제외)이면 Visible을 반환합니다.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string;
        var isNovel = ModelBadgeKeywords.Matches(name, ModelBadgeKeywords.Novel);
        var isUncensored = ModelBadgeKeywords.Matches(name, ModelBadgeKeywords.Uncensored);
        return isNovel && !isUncensored ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>지원하지 않습니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 무검열/검열 해제 모델이면 "무검열 노벨 특화" 배지를 표시합니다.
/// </summary>
public sealed class UncensoredModelBadgeConverter : IValueConverter
{
    /// <summary>무검열 모델이면 Visible을 반환합니다.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ModelBadgeKeywords.Matches(value as string, ModelBadgeKeywords.Uncensored)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>지원하지 않습니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 한글 특화 모델(EXAONE 등)이면 "한글 특화" 배지를 표시합니다.
/// </summary>
public sealed class KoreanModelBadgeConverter : IValueConverter
{
    /// <summary>한글 특화 모델이면 Visible을 반환합니다.</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return ModelBadgeKeywords.Matches(value as string, ModelBadgeKeywords.Korean)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>지원하지 않습니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>
/// 모델이 설치 목록에 있으면 "설치됨" 배지를 표시합니다. (values[0]=모델명, values[1]=설치 목록)
/// </summary>
public sealed class InstalledModelBadgeConverter : IMultiValueConverter
{
    /// <summary>설치된 모델이면 Visible을 반환합니다.</summary>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string name || values[1] is not IEnumerable installed)
        {
            return Visibility.Collapsed;
        }

        foreach (var item in installed)
        {
            if (item is string installedName && string.Equals(installedName, name, StringComparison.OrdinalIgnoreCase))
            {
                return Visibility.Visible;
            }
        }

        return Visibility.Collapsed;
    }

    /// <summary>지원하지 않습니다.</summary>
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
