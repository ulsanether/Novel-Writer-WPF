using System.IO;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 지정한 폴더에서 참고자료 Markdown 파일을 읽어옵니다.
/// </summary>
public sealed class ReferenceLibraryService
{
    /// <summary>
    /// 폴더 안의 모든 .md 파일을 이름순으로 읽어 반환합니다.
    /// </summary>
    /// <param name="folderPath">참고자료 폴더 경로입니다.</param>
    public IReadOnlyList<ReferenceDocument> LoadFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Array.Empty<ReferenceDocument>();
        }

        var documents = new List<ReferenceDocument>();

        foreach (var path in Directory.EnumerateFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly)
                     .OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                documents.Add(new ReferenceDocument
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    FullPath = path,
                    Content = File.ReadAllText(path)
                });
            }
            catch (IOException)
            {
                // 읽을 수 없는 파일은 건너뜁니다.
            }
        }

        return documents;
    }
}
