using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace NovelWriter.Wpf;

/// <summary>
/// Markdown 원문 문자열을 읽기용 <see cref="FlowDocument"/>로 변환합니다.
/// 제목(#), 굵게(**), 목록(-, *) 정도의 경량 서식만 지원합니다.
/// </summary>
public sealed class MarkdownFlowDocumentConverter : IValueConverter
{
    /// <summary>
    /// Markdown 문자열을 FlowDocument로 변환합니다.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var markdown = value as string ?? string.Empty;
        return Render(markdown);
    }

    /// <summary>
    /// 지원하지 않습니다.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;

    private static FlowDocument Render(string markdown)
    {
        // FontSize/Foreground는 지정하지 않아 FlowDocumentScrollViewer의 값(설정 반영)을 상속받습니다.
        var document = new FlowDocument
        {
            PagePadding = new Thickness(4),
            LineHeight = 20
        };

        var lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        List? currentList = null;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();

            if (line.Length == 0)
            {
                currentList = null;
                continue;
            }

            if (line.StartsWith('#'))
            {
                currentList = null;
                var level = 0;
                while (level < line.Length && line[level] == '#')
                {
                    level++;
                }

                var text = line[level..].Trim();
                var heading = new Paragraph
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = level switch { 1 => 22, 2 => 18, _ => 15 },
                    Margin = new Thickness(0, level == 1 ? 4 : 8, 0, 4)
                };
                heading.Inlines.AddRange(ParseInlines(text));
                document.Blocks.Add(heading);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                var text = line[2..];
                if (currentList is null)
                {
                    currentList = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(16, 0, 0, 0) };
                    document.Blocks.Add(currentList);
                }

                var item = new Paragraph { Margin = new Thickness(0) };
                item.Inlines.AddRange(ParseInlines(text));
                currentList.ListItems.Add(new ListItem(item));
                continue;
            }

            currentList = null;
            var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 2) };
            paragraph.Inlines.AddRange(ParseInlines(line));
            document.Blocks.Add(paragraph);
        }

        return document;
    }

    /// <summary>
    /// **굵게** 를 처리하여 인라인 목록으로 변환합니다.
    /// </summary>
    private static IEnumerable<Inline> ParseInlines(string text)
    {
        var inlines = new List<Inline>();
        var parts = text.Split("**");

        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            // 짝수 인덱스는 일반, 홀수 인덱스는 굵게입니다. (** 로 감싼 구간)
            if (i % 2 == 1)
            {
                inlines.Add(new Bold(new Run(parts[i])));
            }
            else
            {
                inlines.Add(new Run(parts[i]));
            }
        }

        if (inlines.Count == 0)
        {
            inlines.Add(new Run(string.Empty));
        }

        return inlines;
    }
}
