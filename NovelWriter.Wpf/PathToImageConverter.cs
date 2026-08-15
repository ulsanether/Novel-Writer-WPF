using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace NovelWriter.Wpf;

/// <summary>
/// 파일 경로 문자열을 이미지 소스로 변환합니다. (파일 잠금 방지 + 캐시 없음)
/// </summary>
public sealed class PathToImageConverter : IValueConverter
{
    /// <summary>
    /// 경로를 BitmapImage로 변환합니다. 파일이 없으면 null입니다.
    /// </summary>
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;      // 파일 즉시 로드 후 해제
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;  // 재생성 시 갱신
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>지원하지 않습니다.</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
