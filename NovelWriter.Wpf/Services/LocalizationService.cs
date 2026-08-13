namespace NovelWriter.Wpf.Services;

/// <summary>
/// 한글/영문 UI 텍스트를 제공합니다.
/// </summary>
public sealed class LocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> Korean = new Dictionary<string, string>
    {
        ["TitlePlaceholder"] = "문서 제목",
        ["ExportButton"] = "DOCX 내보내기",
        ["SaveButton"] = "저장",
        ["AiFixButton"] = "AI 오타 보정",
        ["FocusButton"] = "집중 모드",
        ["ThemeDark"] = "다크",
        ["ThemeLight"] = "라이트",
        ["ThemeCustom"] = "커스텀",
        ["Language"] = "언어",
        ["TimerLabel"] = "타이머(분)",
        ["GoalLabel"] = "일일 목표 단어수",
        ["Stats"] = "통계",
        ["Word"] = "단어",
        ["Character"] = "문자",
        ["Page"] = "페이지",
        ["Paragraph"] = "단락",
        ["Sentence"] = "문장",
        ["Progress"] = "진행률",
        ["Saved"] = "저장 완료",
        ["AutoSaved"] = "자동 저장 완료",
        ["AiFixed"] = "오타 자동 보정 완료"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["TitlePlaceholder"] = "Document title",
        ["ExportButton"] = "Export DOCX",
        ["SaveButton"] = "Save",
        ["AiFixButton"] = "AI typo fix",
        ["FocusButton"] = "Focus mode",
        ["ThemeDark"] = "Dark",
        ["ThemeLight"] = "Light",
        ["ThemeCustom"] = "Custom",
        ["Language"] = "Language",
        ["TimerLabel"] = "Timer (minutes)",
        ["GoalLabel"] = "Daily word goal",
        ["Stats"] = "Stats",
        ["Word"] = "Words",
        ["Character"] = "Characters",
        ["Page"] = "Pages",
        ["Paragraph"] = "Paragraphs",
        ["Sentence"] = "Sentences",
        ["Progress"] = "Progress",
        ["Saved"] = "Saved",
        ["AutoSaved"] = "Autosaved",
        ["AiFixed"] = "AI typo fix completed"
    };

    /// <summary>
    /// 키에 해당하는 다국어 문자열을 반환합니다.
    /// </summary>
    /// <param name="languageCode">언어 코드입니다.</param>
    /// <param name="key">텍스트 키입니다.</param>
    /// <returns>해당 언어 문자열입니다.</returns>
    public string Get(string languageCode, string key)
    {
        var table = languageCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase) ? Korean : English;
        return table.TryGetValue(key, out var value) ? value : key;
    }
}
