namespace NovelWriter.Wpf.Models;

/// <summary>
/// 서랍에 표시되는 참고자료(.md) 한 건을 나타냅니다.
/// </summary>
public sealed class ReferenceDocument
{
    /// <summary>
    /// 표시용 이름(확장자를 제외한 파일명)입니다.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 파일 전체 경로입니다.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// Markdown 원문입니다.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
