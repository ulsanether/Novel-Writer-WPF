using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf;

/// <summary>
/// 에디터 TextBox 위에 오타 구간의 빨간 물결선을 그리는 어도너입니다.
/// </summary>
public sealed class SpellingAdorner : Adorner
{
    private const double Amplitude = 1.3;
    private const double Step = 2.0;

    private readonly RichTextBox _editor;
    private readonly Func<IEnumerable<TypoMark>> _marksProvider;
    private readonly Pen _spellingPen;
    private readonly Pen _contextPen;

    /// <summary>
    /// 어도너를 초기화합니다.
    /// </summary>
    /// <param name="editor">대상 RichTextBox 에디터입니다.</param>
    /// <param name="marksProvider">그릴 오타 마크 목록을 제공하는 콜백입니다.</param>
    public SpellingAdorner(RichTextBox editor, Func<IEnumerable<TypoMark>> marksProvider)
        : base(editor)
    {
        _editor = editor;
        _marksProvider = marksProvider;
        IsHitTestVisible = false;

        _spellingPen = new Pen(new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)), 1.3); // 빨강: 맞춤법
        _spellingPen.Freeze();
        _contextPen = new Pen(new SolidColorBrush(Color.FromRgb(0x29, 0x8A, 0xFF)), 1.3);  // 파랑: AI 문맥
        _contextPen.Freeze();
    }

    /// <summary>
    /// 오타 물결선을 렌더링합니다.
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));

        try
        {
            foreach (var mark in _marksProvider())
            {
                if (mark.Start < 0 || mark.Length <= 0)
                {
                    continue;
                }

                var startPointer = RichTextBoxHelpers.GetPointerAtOffset(_editor, mark.Start);
                var endPointer = RichTextBoxHelpers.GetPointerAtOffset(_editor, mark.End);
                var startRect = startPointer.GetCharacterRect(LogicalDirection.Forward);
                var endRect = endPointer.GetCharacterRect(LogicalDirection.Backward);
                if (startRect.IsEmpty || endRect.IsEmpty)
                {
                    continue;
                }

                var y = startRect.Bottom - 1;
                var x1 = startRect.Left;

                // 여러 시각 줄에 걸치면 시작 줄의 오른쪽 끝까지만 표시합니다.
                var sameLine = System.Math.Abs(startRect.Top - endRect.Top) < 0.5;
                var x2 = sameLine ? endRect.Right : ActualWidth;

                if (x2 <= x1)
                {
                    continue;
                }

                var pen = mark.Kind == TypoKind.Context ? _contextPen : _spellingPen;
                DrawWavyLine(drawingContext, pen, x1, x2, y);
            }
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    private void DrawWavyLine(DrawingContext drawingContext, Pen pen, double x1, double x2, double y)
    {
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(x1, y), false, false);

            var up = true;
            for (var x = x1; x <= x2; x += Step)
            {
                context.LineTo(new Point(x, y + (up ? -Amplitude : Amplitude)), true, true);
                up = !up;
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }
}
