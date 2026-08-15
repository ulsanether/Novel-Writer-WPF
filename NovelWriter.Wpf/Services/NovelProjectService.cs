using System.IO;
using System.Text.Json;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 소설 작품 프로젝트(`.novel`)를 생성·저장·열기 합니다. 작품 하나 = 폴더 하나 + `.novel` 파일 하나.
/// 원고·설계·설정은 `.novel`(JSON)에 통합되고, 이미지·참고자료(.md)는 폴더 하위에 함께 보관됩니다.
/// </summary>
public sealed class NovelProjectService
{
    /// <summary>프로젝트 파일 확장자입니다.</summary>
    public const string Extension = ".novel";

    /// <summary>작품 폴더에 함께 생성되는 유형별 하위 폴더입니다.</summary>
    public static readonly string[] SubFolders =
    {
        "Characters", "Characters/Sheets", "Illustrations", "Backgrounds", "World", "Synopsis", "Descriptions"
    };

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>현재 열려 있는 프로젝트 파일 경로입니다. (없으면 null)</summary>
    public string? CurrentPath { get; private set; }

    /// <summary>현재 프로젝트 폴더입니다. (없으면 null)</summary>
    public string? CurrentFolder => string.IsNullOrWhiteSpace(CurrentPath) ? null : Path.GetDirectoryName(CurrentPath);

    /// <summary>
    /// 지정 폴더에 새 작품을 생성합니다. (작품명 하위 폴더 + 유형별 폴더 + `.novel` 파일)
    /// </summary>
    /// <param name="parentDirectory">작품 폴더를 만들 상위 위치입니다.</param>
    /// <param name="title">작품 제목입니다.</param>
    /// <returns>생성된 프로젝트와 파일 경로입니다.</returns>
    public async Task<(NovelProject project, string path)> CreateAsync(string parentDirectory, string title)
    {
        var safeTitle = MakeSafeName(string.IsNullOrWhiteSpace(title) ? "새 작품" : title.Trim());
        var folder = Path.Combine(parentDirectory, safeTitle);
        Directory.CreateDirectory(folder);
        foreach (var sub in SubFolders)
        {
            Directory.CreateDirectory(Path.Combine(folder, sub.Replace('/', Path.DirectorySeparatorChar)));
        }

        var now = DateTime.UtcNow.ToString("o");
        var project = new NovelProject
        {
            Title = string.IsNullOrWhiteSpace(title) ? "새 작품" : title.Trim(),
            CreatedUtc = now,
            ModifiedUtc = now
        };

        var path = Path.Combine(folder, safeTitle + Extension);
        await SaveAsync(project, path).ConfigureAwait(false);
        CurrentPath = path;
        return (project, path);
    }

    /// <summary>
    /// 프로젝트를 지정 경로에 저장합니다. (수정 시각 갱신)
    /// </summary>
    public async Task SaveAsync(NovelProject project, string path)
    {
        project.ModifiedUtc = DateTime.UtcNow.ToString("o");
        var json = JsonSerializer.Serialize(project, Options);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        CurrentPath = path;
    }

    /// <summary>
    /// 프로젝트 파일을 읽어옵니다. 실패 시 null을 반환합니다.
    /// </summary>
    public NovelProject? Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var project = JsonSerializer.Deserialize<NovelProject>(json);
            if (project is null)
            {
                return null;
            }

            project.Story ??= new StoryProject();
            project.Ai ??= new ProjectAiSettings();
            project.Image ??= new ProjectImageSettings();
            CurrentPath = path;
            return project;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 파일/폴더 이름으로 안전한 문자열을 만듭니다.
    /// </summary>
    private static string MakeSafeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Length > 80 ? name[..80] : name;
    }
}
