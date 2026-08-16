using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace NovelWriter.Wpf.Models;

/// <summary>
/// 등장인물 설정입니다. (Story Bible)
/// </summary>
public partial class StoryCharacter : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _personality = string.Empty;
    [ObservableProperty] private string _goal = string.Empty;
    [ObservableProperty] private string _secret = string.Empty;
    [ObservableProperty] private string _relationships = string.Empty;

    /// <summary>이미지 생성용 외형 프롬프트(영어)입니다. 씬 삽화 일관성에 재사용됩니다.</summary>
    [ObservableProperty] private string _appearancePrompt = string.Empty;

    /// <summary>캐릭터 대표 레퍼런스 이미지 경로입니다.</summary>
    [ObservableProperty] private string _referenceImagePath = string.Empty;

    /// <summary>캐릭터 고정 시드입니다.</summary>
    [ObservableProperty] private long _imageSeed = -1;
}

/// <summary>
/// Scene(작은 이야기 단위) 설정과 본문입니다.
/// </summary>
public partial class SceneNode : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _goal = string.Empty;
    [ObservableProperty] private string _characters = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _conflict = string.Empty;
    [ObservableProperty] private string _result = string.Empty;
    [ObservableProperty] private string _nextLink = string.Empty;
    [ObservableProperty] private string _content = string.Empty;

    /// <summary>씬 삽화 이미지 경로입니다.</summary>
    [ObservableProperty] private string _illustrationPath = string.Empty;

    /// <summary>씬 삽화 생성 프롬프트(영어)입니다.</summary>
    [ObservableProperty] private string _illustrationPrompt = string.Empty;

    /// <summary>씬 삽화 시드입니다.</summary>
    [ObservableProperty] private long _illustrationSeed = -1;
}

/// <summary>
/// 장(Chapter) 설정과 하위 Scene 목록입니다.
/// </summary>
public partial class ChapterNode : ObservableObject
{
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _purpose = string.Empty;
    [ObservableProperty] private string _characters = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _conflict = string.Empty;
    [ObservableProperty] private string _reveal = string.Empty;
    [ObservableProperty] private string _ending = string.Empty;

    /// <summary>이야기 단계(발단·전개·위기·절정·결말)입니다.</summary>
    [ObservableProperty] private string _phase = string.Empty;

    /// <summary>하위 Scene 목록입니다.</summary>
    public ObservableCollection<SceneNode> Scenes { get; set; } = new();
}

/// <summary>
/// 작품 전체 설계 데이터(Story Bible + 시놉시스 + 장/Scene)입니다. DOCX 원고와 분리 저장됩니다.
/// </summary>
public partial class StoryProject : ObservableObject
{
    // ── Story Bible ──
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _genre = string.Empty;
    [ObservableProperty] private string _world = string.Empty;
    [ObservableProperty] private string _era = string.Empty;
    [ObservableProperty] private string _coreEvent = string.Empty;
    [ObservableProperty] private string _ending = string.Empty;
    [ObservableProperty] private string _forbidden = string.Empty;
    [ObservableProperty] private int _chapterCount = 20;

    /// <summary>등장인물 목록입니다.</summary>
    public ObservableCollection<StoryCharacter> Characters { get; set; } = new();

    // ── 시놉시스 ──
    [ObservableProperty] private string _synopsis = string.Empty;

    /// <summary>생성 시 참조할 참고자료 메모(인물·배경 묘사 등)입니다.</summary>
    [ObservableProperty] private string _referenceNotes = string.Empty;

    /// <summary>모든 이미지에 공통 적용할 화풍 프리픽스(영어)입니다.</summary>
    [ObservableProperty] private string _imageStylePrefix = "storybook illustration, soft lighting, detailed";

    // ── 본문 수위(성인) 설정 · 무검열 모델에서만 강하게 반영 ──
    /// <summary>콘텐츠 이용 등급입니다. (전체/12+/15+/18+)</summary>
    [ObservableProperty] private string _contentRating = "전체 이용가";

    /// <summary>선정성 수위입니다. (없음/약함/중간/강함/노골적)</summary>
    [ObservableProperty] private string _sexualLevel = "없음";

    /// <summary>폭력·잔혹 수위입니다. (없음/약함/중간/강함/잔혹)</summary>
    [ObservableProperty] private string _violenceLevel = "없음";

    // ── 장/Scene ──
    /// <summary>장 목록입니다.</summary>
    public ObservableCollection<ChapterNode> Chapters { get; set; } = new();
}
