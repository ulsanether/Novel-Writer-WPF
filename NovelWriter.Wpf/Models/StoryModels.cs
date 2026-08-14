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

    // ── 장/Scene ──
    /// <summary>장 목록입니다.</summary>
    public ObservableCollection<ChapterNode> Chapters { get; set; } = new();
}
