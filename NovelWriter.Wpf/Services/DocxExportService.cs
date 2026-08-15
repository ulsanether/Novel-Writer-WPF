using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WpfDoc = System.Windows.Documents;
using WpfMedia = System.Windows.Media;
using WpfMediaImaging = System.Windows.Media.Imaging;
using WpfControls = System.Windows.Controls;
using WpfText = System.Windows;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 문서를 DOCX 형식으로 내보냅니다.
/// </summary>
public sealed class DocxExportService
{
    private uint _imageSeq;
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
    public Task ExportFlowDocumentAsync(string filePath, string title, WpfDoc.FlowDocument flowDocument, double baseFontSizePx = 30)
    {
        _imageSeq = 0;
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
                AppendInlines(paragraph, wpfParagraph.Inlines, baseFontSizePx, mainPart);
                body.AppendChild(paragraph);
            }
        }

        mainPart.Document.Append(body);
        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    private void AppendInlines(Paragraph paragraph, WpfDoc.InlineCollection inlines, double baseFontSizePx, MainDocumentPart mainPart)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case WpfDoc.Run wpfRun:
                    paragraph.AppendChild(BuildRun(wpfRun, baseFontSizePx));
                    break;
                case WpfDoc.LineBreak:
                    paragraph.AppendChild(new Run(new Break()));
                    break;
                case WpfDoc.InlineUIContainer { Child: WpfControls.Image image } when image.Tag is string imagePath:
                    // 본문 삽입 이미지(⟦IMG:...⟧)를 DOCX에 임베드합니다.
                    AppendImage(paragraph, mainPart, imagePath, image.Source as WpfMediaImaging.BitmapSource);
                    break;
                case WpfDoc.Span span:
                    // Bold/Italic/Underline/Hyperlink 등 컨테이너는 내부 Run을 재귀 처리합니다.
                    AppendInlines(paragraph, span.Inlines, baseFontSizePx, mainPart);
                    break;
            }
        }
    }

    // 이미지 파일을 DOCX에 임베드해 인라인 그림으로 문단에 추가합니다.
    private void AppendImage(Paragraph paragraph, MainDocumentPart mainPart, string tagPath, WpfMediaImaging.BitmapSource? source)
    {
        try
        {
            var resolved = ResolveImagePath(tagPath);
            if (resolved is null || !File.Exists(resolved))
            {
                // 파일이 없으면 토큰 텍스트라도 남겨 경로를 보존합니다.
                paragraph.AppendChild(new Run(new Text($"⟦IMG:{tagPath}⟧") { Space = SpaceProcessingModeValues.Preserve }));
                return;
            }

            var ext = Path.GetExtension(resolved).ToLowerInvariant();
            var partType = ext is ".jpg" or ".jpeg" ? ImagePartType.Jpeg
                : ext == ".gif" ? ImagePartType.Gif
                : ext == ".bmp" ? ImagePartType.Bmp
                : ImagePartType.Png;

            var imagePart = mainPart.AddImagePart(partType);
            using (var stream = File.OpenRead(resolved))
            {
                imagePart.FeedData(stream);
            }

            var relId = mainPart.GetIdOfPart(imagePart);

            // 픽셀 크기 → EMU(914400/inch, 96px/inch → px*9525), 최대 폭 약 6인치로 제한.
            var pxW = source?.PixelWidth > 0 ? source.PixelWidth : 512;
            var pxH = source?.PixelHeight > 0 ? source.PixelHeight : 512;
            long wEmu = (long)pxW * 9525;
            long hEmu = (long)pxH * 9525;
            const long maxW = 5486400; // 6 inch
            if (wEmu > maxW)
            {
                hEmu = (long)(hEmu * (maxW / (double)wEmu));
                wEmu = maxW;
            }

            var id = ++_imageSeq;
            paragraph.AppendChild(new Run(BuildInlineDrawing(relId, wEmu, hEmu, id)));
        }
        catch
        {
            // 임베드 실패 시 무시(문단만 유지)
        }
    }

    // 태그에 저장된 경로(상대/절대)를 실제 파일 경로로 해석합니다.
    private static string? ResolveImagePath(string tagPath)
    {
        if (string.IsNullOrWhiteSpace(tagPath))
        {
            return null;
        }

        if (Path.IsPathRooted(tagPath))
        {
            return tagPath;
        }

        var baseFolder = NovelWriter.Wpf.RichTextBoxHelpers.ImageBaseFolder;
        return string.IsNullOrWhiteSpace(baseFolder) ? tagPath : Path.Combine(baseFolder, tagPath);
    }

    // 인라인 그림(Drawing) 요소를 생성합니다.
    private static Drawing BuildInlineDrawing(string relationshipId, long widthEmu, long heightEmu, uint id)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                new DW.DocProperties { Id = id, Name = $"Picture {id}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = id, Name = $"Picture {id}" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0, Y = 0 },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });
    }

    private static Run BuildRun(WpfDoc.Run wpfRun, double baseFontSizePx)
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

        if (!double.IsNaN(wpfRun.FontSize) && wpfRun.FontSize > 0 && baseFontSizePx > 0)
        {
            // 편집기 기본 크기(baseFontSizePx)가 11pt(sz 22)가 되도록 변환합니다.
            // sz(하프포인트) = px / base * 22. 화면을 키워도 기본 텍스트는 11pt로 저장됩니다.
            var halfPoints = (int)System.Math.Round(wpfRun.FontSize / baseFontSizePx * 22.0);
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
