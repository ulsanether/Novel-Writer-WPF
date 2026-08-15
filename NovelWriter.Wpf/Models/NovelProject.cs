namespace NovelWriter.Wpf.Models;

/// <summary>
/// 하나의 소설 작품을 통합 관리하는 프로젝트입니다.
/// 원고·스토리 설계·AI/이미지 설정을 한 개의 `.novel`(JSON) 파일에 담고,
/// 이미지·참고자료(.md)는 프로젝트 폴더 하위에 함께 보관합니다.
/// </summary>
public sealed class NovelProject
{
    /// <summary>파일 포맷 버전입니다.</summary>
    public int FormatVersion { get; set; } = 1;

    /// <summary>소설 제목입니다.</summary>
    public string Title { get; set; } = "새 작품";

    /// <summary>작가/메모입니다.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>생성 시각(ISO 8601, UTC)입니다.</summary>
    public string CreatedUtc { get; set; } = string.Empty;

    /// <summary>수정 시각(ISO 8601, UTC)입니다.</summary>
    public string ModifiedUtc { get; set; } = string.Empty;

    /// <summary>본문 원고(평문)입니다.</summary>
    public string Manuscript { get; set; } = string.Empty;

    /// <summary>계층형 스토리 설계입니다. (스토리 플래너 데이터)</summary>
    public StoryProject Story { get; set; } = new();

    /// <summary>이 작품에 적용할 AI(LLM) 설정입니다.</summary>
    public ProjectAiSettings Ai { get; set; } = new();

    /// <summary>이 작품에 적용할 이미지 생성 설정입니다.</summary>
    public ProjectImageSettings Image { get; set; } = new();
}

/// <summary>작품별 AI(LLM) 설정입니다.</summary>
public sealed class ProjectAiSettings
{
    /// <summary>사용할 로컬 모델 이름입니다.</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>OpenAI 호환 서버 주소입니다.</summary>
    public string BaseUrl { get; set; } = string.Empty;
}

/// <summary>작품별 이미지 생성 설정입니다. (ComfyUI)</summary>
public sealed class ProjectImageSettings
{
    /// <summary>ComfyUI 서버 주소입니다.</summary>
    public string ComfyUiBaseUrl { get; set; } = string.Empty;

    /// <summary>ComfyUI 설치 폴더 경로입니다.</summary>
    public string ComfyUiPath { get; set; } = string.Empty;

    /// <summary>사용할 체크포인트(모델) 파일명입니다.</summary>
    public string Checkpoint { get; set; } = string.Empty;

    /// <summary>하드웨어 프로파일 키입니다.</summary>
    public string Hardware { get; set; } = string.Empty;

    /// <summary>화풍(이미지 스타일) 설정입니다.</summary>
    public ImageStyleSettings Style { get; set; } = new();
}
