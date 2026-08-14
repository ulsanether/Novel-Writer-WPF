using System.IO;
using System.Text.Json;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 작품 설계 데이터(StoryProject)를 JSON 파일로 저장하고 읽습니다. (DOCX 원고와 분리)
/// </summary>
public sealed class StoryProjectService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    /// <summary>
    /// 서비스를 초기화합니다.
    /// </summary>
    /// <param name="dataDirectory">데이터 디렉터리 경로입니다.</param>
    public StoryProjectService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _path = Path.Combine(dataDirectory, "story_project.json");
    }

    /// <summary>
    /// 저장된 작품 설계를 읽어옵니다. 없으면 새 프로젝트를 반환합니다.
    /// </summary>
    public StoryProject Load()
    {
        if (!File.Exists(_path))
        {
            return new StoryProject();
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<StoryProject>(json) ?? new StoryProject();
        }
        catch
        {
            return new StoryProject();
        }
    }

    /// <summary>
    /// 작품 설계를 기본 파일에 저장합니다.
    /// </summary>
    public async Task SaveAsync(StoryProject project)
    {
        await SaveToPathAsync(project, _path).ConfigureAwait(false);
    }

    /// <summary>
    /// 작품 설계를 지정한 경로에 저장합니다.
    /// </summary>
    public async Task SaveToPathAsync(StoryProject project, string path)
    {
        var json = JsonSerializer.Serialize(project, Options);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    /// <summary>
    /// 지정한 경로의 작품 설계를 읽어옵니다. 실패 시 null을 반환합니다.
    /// </summary>
    public StoryProject? LoadFromPath(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<StoryProject>(json);
        }
        catch
        {
            return null;
        }
    }
}
