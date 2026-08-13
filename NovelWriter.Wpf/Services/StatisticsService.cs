using System.Text.RegularExpressions;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 문서 내용을 분석하여 통계를 계산합니다.
/// </summary>
public sealed partial class StatisticsService
{
    private const int CharactersPerPage = 1800;

    /// <summary>
    /// 현재 문서의 통계를 계산합니다.
    /// </summary>
    /// <param name="content">분석할 문서 내용입니다.</param>
    /// <returns>계산된 통계 결과입니다.</returns>
    public WritingStatistics Calculate(string? content)
    {
        var text = content ?? string.Empty;
        var words = SplitWordsRegex().Matches(text).Count;
        var paragraphs = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length;
        var sentences = SentenceRegex().Matches(text).Count;
        var characters = text.Length;
        var pages = Math.Max(1, (int)Math.Ceiling(characters / (double)CharactersPerPage));

        return new WritingStatistics
        {
            WordCount = words,
            CharacterCount = characters,
            PageCount = pages,
            ParagraphCount = paragraphs,
            SentenceCount = sentences
        };
    }

    [GeneratedRegex(@"\S+", RegexOptions.Compiled)]
    private static partial Regex SplitWordsRegex();

    [GeneratedRegex(@"[.!?]+", RegexOptions.Compiled)]
    private static partial Regex SentenceRegex();
}
