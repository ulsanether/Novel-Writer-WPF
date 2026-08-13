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
    /// 집중 타이머 목표 시간을 분 단위로 가져오거나 설정합니다.
    /// </summary>
    public int FocusMinutes { get; set; } = 25;
}
