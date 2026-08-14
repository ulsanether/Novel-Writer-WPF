using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 외부 문서를 현재 편집기로 불러옵니다.
/// </summary>
public sealed class DocumentImportService
{
    /// <summary>
    /// 파일 경로에서 문서 제목과 본문을 읽어옵니다.
    /// </summary>
    /// <param name="filePath">불러올 파일 경로입니다.</param>
    /// <returns>문서 제목과 본문입니다.</returns>
    public async Task<(string Title, string Content)> ImportAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".txt" or ".md" => (Path.GetFileNameWithoutExtension(filePath), await File.ReadAllTextAsync(filePath).ConfigureAwait(false)),
            ".docx" => await LoadDocxAsync(filePath).ConfigureAwait(false),
            _ => throw new NotSupportedException($"지원하지 않는 파일 형식입니다: {filePath}")
        };
    }

    private static Task<(string Title, string Content)> LoadDocxAsync(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var paragraphs = document.MainDocumentPart?.Document.Body?
            .Elements<Paragraph>()
            .Select(paragraph => paragraph.InnerText ?? string.Empty)
            .ToList() ?? [];

        var title = Path.GetFileNameWithoutExtension(filePath);
        var contentStartIndex = 0;

        if (paragraphs.Count > 0 && !string.IsNullOrWhiteSpace(paragraphs[0]))
        {
            title = paragraphs[0].Trim();
            contentStartIndex = paragraphs.Count > 1 && string.IsNullOrWhiteSpace(paragraphs[1]) ? 2 : 1;
        }

        var content = string.Join(Environment.NewLine, paragraphs.Skip(contentStartIndex)).TrimEnd();
        return Task.FromResult((title, content));
    }
}
