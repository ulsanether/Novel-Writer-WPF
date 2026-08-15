using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace NovelWriter.Wpf;

/// <summary>
/// RichTextBox의 평문(plain text) 추출과 문자 오프셋 ↔ TextPointer 변환을 제공합니다.
/// 문단/줄바꿈은 모두 '\n' 한 글자로 취급하여 오프셋을 일관되게 유지합니다.
/// 본문 삽입 이미지는 평문에서 토큰 <c>⟦IMG:경로⟧</c>으로 표현되고, 편집기에서는 실제 이미지로 렌더링됩니다.
/// </summary>
public static class RichTextBoxHelpers
{
    private static readonly Regex ImageTokenRegex = new(@"⟦IMG:(.+?)⟧", RegexOptions.Compiled);

    /// <summary>
    /// 상대경로 이미지 토큰을 해석할 기준 폴더입니다. (현재 작품 폴더) — 이식성을 위해 상대경로 저장을 지원합니다.
    /// </summary>
    public static string? ImageBaseFolder { get; set; }

    /// <summary>이미지 경로를 평문 토큰으로 만듭니다.</summary>
    public static string ImageToken(string path) => $"⟦IMG:{path}⟧";

    /// <summary>토큰 경로(상대/절대)를 실제 파일 경로로 해석합니다.</summary>
    public static string ResolveImagePath(string tokenPath)
    {
        if (string.IsNullOrWhiteSpace(tokenPath) || System.IO.Path.IsPathRooted(tokenPath))
        {
            return tokenPath;
        }

        return string.IsNullOrWhiteSpace(ImageBaseFolder)
            ? tokenPath
            : System.IO.Path.Combine(ImageBaseFolder, tokenPath);
    }

    /// <summary>절대 경로를 기준 폴더 기준 상대경로로 만듭니다. (폴더 밖이면 절대경로 유지)</summary>
    public static string ToPortablePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath) || string.IsNullOrWhiteSpace(ImageBaseFolder))
        {
            return absolutePath;
        }

        try
        {
            var baseFull = System.IO.Path.GetFullPath(ImageBaseFolder);
            var full = System.IO.Path.GetFullPath(absolutePath);
            if (full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
            {
                var rel = System.IO.Path.GetRelativePath(baseFull, full);
                // DOCX/JSON 이식성을 위해 '/'로 통일
                return rel.Replace('\\', '/');
            }
        }
        catch
        {
            // 무시
        }

        return absolutePath;
    }

    // InlineUIContainer(이미지)의 평문 토큰을 반환합니다. (경로는 Image.Tag에 보관)
    private static string? TokenForContainer(InlineUIContainer container)
        => container.Child is Image img && img.Tag is string path && !string.IsNullOrEmpty(path)
            ? ImageToken(path)
            : null;

    /// <summary>
    /// 문서의 평문을 반환합니다. (줄바꿈은 '\n', 이미지는 토큰)
    /// </summary>
    public static string GetPlainText(RichTextBox richTextBox)
    {
        var builder = new StringBuilder();
        var pointer = richTextBox.Document.ContentStart;

        while (pointer is not null)
        {
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                builder.Append(pointer.GetTextInRun(LogicalDirection.Forward));
            }
            else if (context == TextPointerContext.ElementStart
                     && pointer.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer container
                     && TokenForContainer(container) is { } token)
            {
                builder.Append(token);
            }
            else if (context == TextPointerContext.ElementStart
                     && pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
            {
                builder.Append('\n');
            }
            else if (context == TextPointerContext.ElementEnd && pointer.Parent is Paragraph)
            {
                builder.Append('\n');
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        // 마지막 문단 끝에 붙는 개행 하나를 제거합니다.
        return builder.Length > 0 && builder[^1] == '\n'
            ? builder.ToString(0, builder.Length - 1)
            : builder.ToString();
    }

    /// <summary>
    /// 문자 오프셋에 해당하는 TextPointer를 반환합니다. (GetPlainText 규칙과 동일)
    /// </summary>
    public static TextPointer GetPointerAtOffset(RichTextBox richTextBox, int targetOffset)
    {
        var pointer = richTextBox.Document.ContentStart;
        var offset = 0;

        while (pointer is not null)
        {
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var run = pointer.GetTextInRun(LogicalDirection.Forward);
                if (offset + run.Length >= targetOffset)
                {
                    return pointer.GetPositionAtOffset(targetOffset - offset, LogicalDirection.Forward) ?? pointer;
                }

                offset += run.Length;
            }
            else if (context == TextPointerContext.ElementStart
                     && pointer.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer container
                     && TokenForContainer(container) is { } token)
            {
                if (offset + token.Length >= targetOffset)
                {
                    return pointer;
                }

                offset += token.Length;
            }
            else if ((context == TextPointerContext.ElementStart
                      && pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
                     || (context == TextPointerContext.ElementEnd && pointer.Parent is Paragraph))
            {
                if (offset >= targetOffset)
                {
                    return pointer;
                }

                offset += 1;
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        return richTextBox.Document.ContentEnd;
    }

    /// <summary>
    /// TextPointer에 해당하는 문자 오프셋을 반환합니다. (GetPlainText 규칙과 동일)
    /// </summary>
    public static int GetOffset(RichTextBox richTextBox, TextPointer target)
    {
        var pointer = richTextBox.Document.ContentStart;
        var offset = 0;

        while (pointer is not null && pointer.CompareTo(target) < 0)
        {
            var context = pointer.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var run = pointer.GetTextInRun(LogicalDirection.Forward);
                var runEnd = pointer.GetPositionAtOffset(run.Length, LogicalDirection.Forward);
                if (runEnd is not null && runEnd.CompareTo(target) <= 0)
                {
                    offset += run.Length;
                }
                else
                {
                    var distance = pointer.GetOffsetToPosition(target);
                    offset += Math.Max(0, Math.Min(run.Length, distance));
                    break;
                }
            }
            else if (context == TextPointerContext.ElementStart
                     && pointer.GetAdjacentElement(LogicalDirection.Forward) is InlineUIContainer container
                     && TokenForContainer(container) is { } token)
            {
                offset += token.Length;
            }
            else if ((context == TextPointerContext.ElementStart
                      && pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak)
                     || (context == TextPointerContext.ElementEnd && pointer.Parent is Paragraph))
            {
                offset += 1;
            }

            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }

        return offset;
    }

    /// <summary>
    /// 평문 텍스트를 문서로 설정합니다. (줄바꿈 '\n' → LineBreak, 이미지 토큰 → 실제 이미지)
    /// </summary>
    public static void SetPlainText(RichTextBox richTextBox, string text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        // 대용량 문서에서 문단(Paragraph)을 수천 개 만들면 매우 느리므로,
        // 단일 문단 안에서 LineBreak로 줄바꿈을 표현합니다.
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        for (var i = 0; i < lines.Length; i++)
        {
            AppendLineWithImages(paragraph, lines[i]);
            if (i < lines.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        richTextBox.Document.Blocks.Clear();
        richTextBox.Document.Blocks.Add(paragraph);
    }

    // 한 줄을 텍스트/이미지 토큰으로 나눠 Inline으로 추가합니다.
    private static void AppendLineWithImages(Paragraph paragraph, string line)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains("⟦IMG:", StringComparison.Ordinal))
        {
            paragraph.Inlines.Add(new Run(line));
            return;
        }

        var lastIndex = 0;
        foreach (Match match in ImageTokenRegex.Matches(line))
        {
            if (match.Index > lastIndex)
            {
                paragraph.Inlines.Add(new Run(line[lastIndex..match.Index]));
            }

            var path = match.Groups[1].Value;
            var inline = CreateImageInline(path);
            paragraph.Inlines.Add(inline);
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < line.Length)
        {
            paragraph.Inlines.Add(new Run(line[lastIndex..]));
        }
    }

    // 이미지 토큰 경로(상대/절대)로 InlineUIContainer를 만듭니다. 파일이 없으면 토큰 텍스트로 대체(경로 보존).
    // Tag에는 토큰 경로 원본(상대일 수 있음)을 저장해 평문 복원 시 그대로 되돌립니다.
    private static Inline CreateImageInline(string tokenPath)
    {
        try
        {
            var resolved = ResolveImagePath(tokenPath);
            if (System.IO.File.Exists(resolved))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bitmap.UriSource = new Uri(resolved);
                bitmap.EndInit();
                bitmap.Freeze();

                var image = new Image
                {
                    Source = bitmap,
                    MaxWidth = 480,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Tag = tokenPath // 평문 복원용 경로(상대/절대 원본)
                };

                return new InlineUIContainer(image);
            }
        }
        catch
        {
            // 무시하고 토큰 텍스트로 대체
        }

        return new Run(ImageToken(tokenPath));
    }
}
