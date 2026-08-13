namespace NovelWriter.Wpf.Services;

/// <summary>
/// 간단한 AI 형태 오타 보정 기능을 제공합니다.
/// </summary>
public sealed class TypoCorrectionService
{
    private static readonly IReadOnlyDictionary<string, string> TypoDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["잇습니다"] = "있습니다",
        ["됬다"] = "됐다",
        ["않돼"] = "안 돼",
        ["업습니다"] = "없습니다",
        ["teh"] = "the",
        ["wirte"] = "write"
    };

    /// <summary>
    /// 텍스트 오타를 비동기 보정합니다.
    /// </summary>
    /// <param name="content">원본 문서 내용입니다.</param>
    /// <returns>보정된 문서 내용입니다.</returns>
    public Task<string> CorrectAsync(string? content)
    {
        var result = content ?? string.Empty;
        foreach (var pair in TypoDictionary)
        {
            result = result.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return Task.FromResult(result);
    }
}
