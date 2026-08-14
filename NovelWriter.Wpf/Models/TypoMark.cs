namespace NovelWriter.Wpf.Models;

/// <summary>
/// 오타 표시의 종류입니다.
/// </summary>
public enum TypoKind
{
    /// <summary>Hunspell 맞춤법 오류(빨간 밑줄)입니다.</summary>
    Spelling,

    /// <summary>AI 문맥/문법 오류(파란 밑줄)입니다.</summary>
    Context
}

/// <summary>
/// 문서 내에서 오타로 표시된 한 구간을 나타냅니다.
/// </summary>
public sealed class TypoMark
{
    /// <summary>
    /// 표시 종류입니다.
    /// </summary>
    public TypoKind Kind { get; set; } = TypoKind.Spelling;

    /// <summary>
    /// 오타 시작 문자 인덱스입니다.
    /// </summary>
    public int Start { get; set; }

    /// <summary>
    /// 오타 길이(문자 수)입니다.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// 틀린 표현입니다.
    /// </summary>
    public string Wrong { get; set; } = string.Empty;

    /// <summary>
    /// 대표 교정 제안입니다. (AI 사전 매핑 등에서 사용)
    /// </summary>
    public string Right { get; set; } = string.Empty;

    /// <summary>
    /// 교정 추천 목록입니다. (우클릭 메뉴에 표시)
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 오타 끝(배타적) 인덱스입니다.
    /// </summary>
    public int End => Start + Length;
}
