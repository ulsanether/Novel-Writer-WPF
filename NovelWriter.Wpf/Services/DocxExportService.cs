using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WpfDoc = System.Windows.Documents;
using WpfMedia = System.Windows.Media;
using WpfText = System.Windows;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 문서를 DOCX 형식으로 내보냅니다.
/// </summary>
public sealed class DocxExportService
{
    /// <summary>
    /// 제목과 평문 본문을 DOCX 파일로 저장합니다. (서식 없음)
    /// </summary>
    public Task ExportAsync(string filePath, string title, string content)
    {
        using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        body.AppendChild(new Paragraph(new Run(new Text(title ?? "Untitled"))));

        // 개행은 '\n' 기준으로 문단을 나눕니다.
        var normalized = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var line in normalized.Split('\n'))
        {
            body.AppendChild(new Paragraph(new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve })));
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    /// <summary>
    /// RichTextBox의 FlowDocument를 서식(굵게·기울임·밑줄·색·크기·하이라이트)까지 DOCX로 저장합니다.
    /// </summary>
    public Task ExportFlowDocumentAsync(string filePath, string title, WpfDoc.FlowDocument flowDocument)
    {
        using var document = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = new Body();

        // 제목 문단 (굵게)
        var titleRun = new Run(new Text(title ?? "Untitled"));
        titleRun.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "36" });
        body.AppendChild(new Paragraph(titleRun));

        foreach (var block in flowDocument.Blocks)
        {
            if (block is WpfDoc.Paragraph wpfParagraph)
            {
                var paragraph = new Paragraph();
                AppendInlines(paragraph, wpfParagraph.Inlines);
                body.AppendChild(paragraph);
            }
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    private static void AppendInlines(Paragraph paragraph, WpfDoc.InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case WpfDoc.Run wpfRun:
                    paragraph.AppendChild(BuildRun(wpfRun));
                    break;
                case WpfDoc.LineBreak:
                    paragraph.AppendChild(new Run(new Break()));
                    break;
                case WpfDoc.Span span:
                    // Bold/Italic/Underline/Hyperlink 등 컨테이너는 내부 Run을 재귀 처리합니다.
                    AppendInlines(paragraph, span.Inlines);
                    break;
            }
        }
    }

    private static Run BuildRun(WpfDoc.Run wpfRun)
    {
        var properties = new RunProperties();

        if (wpfRun.FontWeight == WpfText.FontWeights.Bold)
        {
            properties.Append(new Bold());
        }

        if (wpfRun.FontStyle == WpfText.FontStyles.Italic)
        {
            properties.Append(new Italic());
        }

        if (wpfRun.TextDecorations is { Count: > 0 })
        {
            properties.Append(new Underline { Val = UnderlineValues.Single });
        }

        if (wpfRun.Foreground is WpfMedia.SolidColorBrush foreground)
        {
            properties.Append(new Color { Val = ToHex(foreground.Color) });
        }

        if (!double.IsNaN(wpfRun.FontSize) && wpfRun.FontSize > 0)
        {
            // WPF FontSize(1/96인치 px) → DOCX sz(하프포인트) : px * 72/96 * 2 = px * 1.5
            var halfPoints = (int)System.Math.Round(wpfRun.FontSize * 1.5);
            properties.Append(new FontSize { Val = halfPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        }

        if (wpfRun.Background is WpfMedia.SolidColorBrush background)
        {
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = ToHex(background.Color) });
        }

        var run = new Run { RunProperties = properties };
        run.AppendChild(new Text(wpfRun.Text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    private static string ToHex(WpfMedia.Color color)
        => $"{color.R:X2}{color.G:X2}{color.B:X2}";
}
