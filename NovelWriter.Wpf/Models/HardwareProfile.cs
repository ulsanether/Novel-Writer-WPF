namespace NovelWriter.Wpf.Models;

/// <summary>
/// 이미지 생성 서버 실행 시 적용할 하드웨어(그래픽카드 VRAM) 프로파일입니다.
/// </summary>
/// <param name="Key">저장용 식별자입니다.</param>
/// <param name="DisplayName">표시 이름입니다.</param>
/// <param name="ComfyArgs">ComfyUI 실행 인자입니다.</param>
/// <param name="A1111Args">A1111 COMMANDLINE_ARGS 추가 인자입니다.</param>
/// <param name="Note">안내 문구입니다.</param>
public sealed record HardwareProfile(
    string Key,
    string DisplayName,
    string ComfyArgs,
    string A1111Args,
    string Note);
