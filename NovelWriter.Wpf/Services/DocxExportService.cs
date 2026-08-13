using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 문서를 DOCX 형식으로 내보냅니다.
/// </summary>
public sealed class DocxExportService
{
    /// <summary>
    /// 제목과 본문을 DOCX 파일로 저장합니다.
    /// </summary>
    /// <param name="filePath">저장할 파일 경로입니다.</param>
    /// <param name="title">문서 제목입니다.</param>
    /// <param name="content">문서 내용입니다.</param>
    public Task ExportAsync(string filePath, string title, string content)
    {
        using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        body.AppendChild(new Paragraph(new Run(new Text(title ?? "Untitled"))));
        body.AppendChild(new Paragraph(new Run(new Text(string.Empty))));

        foreach (var paragraph in (content ?? string.Empty).Split(Environment.NewLine))
        {
            body.AppendChild(new Paragraph(new Run(new Text(paragraph) { Space = SpaceProcessingModeValues.Preserve })));
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
        return Task.CompletedTask;
    }
}
