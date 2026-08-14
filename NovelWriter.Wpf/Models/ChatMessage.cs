namespace NovelWriter.Wpf.Models;

/// <summary>
/// AI 채팅 서랍에 표시되는 대화 한 줄을 나타냅니다.
/// </summary>
public sealed class ChatMessage
{
    /// <summary>
    /// 사용자가 보낸 메시지이면 true, AI 응답이면 false입니다.
    /// </summary>
    public bool IsUser { get; set; }

    /// <summary>
    /// 메시지 본문입니다.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
