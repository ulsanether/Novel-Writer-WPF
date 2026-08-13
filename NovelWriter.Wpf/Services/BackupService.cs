using System.IO;
using System.Linq;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 로컬 백업 파일을 생성합니다.
/// </summary>
public sealed class BackupService
{
    private readonly string _backupDirectory;

    /// <summary>
    /// 백업 서비스를 초기화합니다.
    /// </summary>
    /// <param name="backupDirectory">백업 디렉터리 경로입니다.</param>
    public BackupService(string backupDirectory)
    {
        _backupDirectory = backupDirectory;
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// 현재 문서 백업 파일을 생성합니다.
    /// </summary>
    /// <param name="title">문서 제목입니다.</param>
    /// <param name="content">문서 내용입니다.</param>
    public async Task CreateBackupAsync(string title, string content)
    {
        var safeTitle = string.Concat((title ?? "Untitled").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = "Untitled";
        }

        var filePath = Path.Combine(_backupDirectory, $"{safeTitle}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        await File.WriteAllTextAsync(filePath, content ?? string.Empty).ConfigureAwait(false);
    }
}
