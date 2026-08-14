namespace NovelWriter.Wpf.Models;

/// <summary>
/// 애플리케이션 사용자 설정 정보를 나타냅니다.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// 기본 테마를 가져오거나 설정합니다.
    /// </summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// 기본 언어 코드를 가져오거나 설정합니다.
    /// </summary>
    public string LanguageCode { get; set; } = "ko-KR";

    /// <summary>
    /// 커스텀 배경색을 가져오거나 설정합니다.
    /// </summary>
    public string CustomBackgroundHex { get; set; } = "#FF101010";

    /// <summary>
    /// 커스텀 전경색을 가져오거나 설정합니다.
    /// </summary>
    public string CustomForegroundHex { get; set; } = "#FFF0F0F0";

    /// <summary>
    /// 자동 저장 주기를 초 단위로 가져오거나 설정합니다.
    /// </summary>
    public int AutoSaveSeconds { get; set; } = 30;

    /// <summary>
    /// 일일 단어 목표를 가져오거나 설정합니다.
    /// </summary>
    public int DailyWordGoal { get; set; } = 1000;

    /// <summary>
    /// AI 보정에 사용할 로컬 모델 이름을 가져오거나 설정합니다. (예: exaone3.5:2.4b, exaone3.5:7.8b)
    /// </summary>
    public string AiModel { get; set; } = "exaone3.5:7.8b";

    /// <summary>
    /// OpenAI 호환 로컬 서버 주소를 가져오거나 설정합니다. (Ollama 기본값)
    /// </summary>
    public string AiBaseUrl { get; set; } = "http://localhost:11434/v1";

    /// <summary>
    /// 참고자료(.md) 폴더 경로를 가져오거나 설정합니다.
    /// </summary>
    public string ReferenceFolder { get; set; } = string.Empty;

    /// <summary>
    /// 자동 저장 사용 여부를 가져오거나 설정합니다.
    /// </summary>
    public bool AutoSaveEnabled { get; set; } = true;

    /// <summary>
    /// 상단 메뉴 폰트 크기를 가져오거나 설정합니다.
    /// </summary>
    public double MenuFontSize { get; set; } = 13;

    /// <summary>
    /// 참고자료 폰트 크기를 가져오거나 설정합니다.
    /// </summary>
    public double ReferenceFontSize { get; set; } = 14;

    /// <summary>
    /// 참고자료 글자 색(ARGB hex)을 가져오거나 설정합니다.
    /// </summary>
    public string ReferenceForegroundHex { get; set; } = "#FFDDDDDD";

    /// <summary>
    /// 상단 툴바 아이콘 크기를 가져오거나 설정합니다.
    /// </summary>
    public double ToolbarIconSize { get; set; } = 28;

    /// <summary>
    /// AI 어시스턴트(채팅) 폰트 크기를 가져오거나 설정합니다.
    /// </summary>
    public double ChatFontSize { get; set; } = 14;

    /// <summary>
    /// AI 어시스턴트(채팅) 배경색(ARGB hex)을 가져오거나 설정합니다.
    /// </summary>
    public string ChatBackgroundHex { get; set; } = "#FF1E1E1E";
}
