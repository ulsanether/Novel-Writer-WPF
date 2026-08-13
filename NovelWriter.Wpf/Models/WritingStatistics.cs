namespace NovelWriter.Wpf.Models;

/// <summary>
/// 문서 통계 정보를 나타냅니다.
/// </summary>
public sealed class WritingStatistics
{
    /// <summary>
    /// 단어 수를 가져오거나 설정합니다.
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// 문자 수를 가져오거나 설정합니다.
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// 페이지 수를 가져오거나 설정합니다.
    /// </summary>
    public int PageCount { get; set; }

    /// <summary>
    /// 단락 수를 가져오거나 설정합니다.
    /// </summary>
    public int ParagraphCount { get; set; }

    /// <summary>
    /// 문장 수를 가져오거나 설정합니다.
    /// </summary>
    public int SentenceCount { get; set; }
}
