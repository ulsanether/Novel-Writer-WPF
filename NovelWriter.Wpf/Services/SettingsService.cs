using System.Text.Json;
using System.IO;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 사용자 설정 파일을 읽고 저장합니다.
/// </summary>
public sealed class SettingsService
{
    private readonly string _settingsPath;

    /// <summary>
    /// 설정 서비스 초기화.
    /// </summary>
    /// <param name="dataDirectory">데이터 디렉터리 경로입니다.</param>
    public SettingsService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
    }

    /// <summary>
    /// 설정 파일을 읽어옵니다.
    /// </summary>
    /// <returns>사용자 설정입니다.</returns>
    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    /// <summary>
    /// 설정 파일을 저장합니다.
    /// </summary>
    /// <param name="settings">저장할 설정 객체입니다.</param>
    public async Task SaveAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_settingsPath, json).ConfigureAwait(false);
    }
}
