using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace NovelWriter.Wpf;

/// <summary>
/// RichTextBox의 평문(plain text) 추출과 문자 오프셋 ↔ TextPointer 변환을 제공합니다.
/// 문단/줄바꿈은 모두 '\n' 한 글자로 취급하여 오프셋을 일관되게 유지합니다.
/// </summary>
public static class RichTextBoxHelpers
{
    /// <summary>
    /// 문서의 평문을 반환합니다. (줄바꿈은 '\n')
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
    /// 평문 텍스트를 문서로 설정합니다. (줄바꿈 '\n' → LineBreak)
    /// </summary>
    public static void SetPlainText(RichTextBox richTextBox, string text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        // 대용량 문서에서 문단(Paragraph)을 수천 개 만들면 매우 느리므로,
        // 단일 문단 안에서 LineBreak로 줄바꿈을 표현합니다.
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.Inlines.Add(new Run(lines[i]));
            if (i < lines.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        richTextBox.Document.Blocks.Clear();
        richTextBox.Document.Blocks.Add(paragraph);
    }
}
