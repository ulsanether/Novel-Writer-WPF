using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelWriter.Wpf.Models;
using NovelWriter.Wpf.Services;

namespace NovelWriter.Wpf.ViewModels;

/// <summary>
/// 스토리 플래너(계층형 스토리 설계) 뷰모델입니다.
/// </summary>
public partial class StoryPlannerViewModel : ObservableObject
{
    private readonly StoryProjectService _projectService;
    private readonly StoryPlannerService _plannerService;
    private readonly ReferenceLibraryService _referenceLibraryService;
    private readonly string _referenceFolder;

    /// <summary>
    /// Scene 본문을 메인 에디터에 삽입하기 위한 콜백입니다.
    /// </summary>
    public Action<string>? InsertToEditor { get; set; }

    /// <summary>"다른 이름으로 저장" 경로 선택 콜백입니다.</summary>
    public Func<Task<string?>>? SaveAsPathResolver { get; set; }

    /// <summary>"열기" 경로 선택 콜백입니다.</summary>
    public Func<Task<string?>>? OpenPathResolver { get; set; }

    /// <summary>메인 에디터의 현재 원고를 가져오는 콜백입니다. (역분석용)</summary>
    public Func<string>? GetManuscript { get; set; }

    /// <summary>덮어쓰기 전 사용자 확인 콜백입니다. (true=진행)</summary>
    public Func<string, bool>? ConfirmOverwrite { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public StoryPlannerViewModel(
        StoryProject project,
        StoryProjectService projectService,
        StoryPlannerService plannerService,
        ReferenceLibraryService referenceLibraryService,
        string referenceFolder)
    {
        _project = project;
        _projectService = projectService;
        _plannerService = plannerService;
        _referenceLibraryService = referenceLibraryService;
        _referenceFolder = referenceFolder ?? string.Empty;
        LoadReferences();
    }

    /// <summary>참고자료 폴더의 문서 목록입니다.</summary>
    public ObservableCollection<ReferenceDocument> References { get; } = new();

    [ObservableProperty]
    private ReferenceDocument? _selectedReference;

    /// <summary>참고자료 폴더를 다시 스캔합니다.</summary>
    [RelayCommand]
    private void LoadReferences()
    {
        References.Clear();
        foreach (var document in _referenceLibraryService.LoadFolder(_referenceFolder))
        {
            References.Add(document);
        }
    }

    /// <summary>선택한 참고자료에서 등장인물을 추출해 추가합니다.</summary>
    [RelayCommand]
    private async Task ImportCharacterAsync()
    {
        if (SelectedReference is null)
        {
            return;
        }

        var reference = SelectedReference;
        await RunAsync($"'{reference.Name}'에서 인물을 추출하는 중...", async () =>
        {
            var character = await _plannerService.ExtractCharacterAsync(reference.Content);
            if (character is not null)
            {
                Project.Characters.Add(character);
            }
        });
    }

    /// <summary>선택한 참고자료를 생성 참조(ReferenceNotes)에 추가합니다.</summary>
    [RelayCommand]
    private void IncludeReference()
    {
        if (SelectedReference is null)
        {
            return;
        }

        var block = $"# {SelectedReference.Name}\n{SelectedReference.Content}";
        Project.ReferenceNotes = string.IsNullOrWhiteSpace(Project.ReferenceNotes)
            ? block
            : Project.ReferenceNotes + "\n\n" + block;
        StatusMessage = $"'{SelectedReference.Name}'을(를) 생성 참조에 추가했습니다.";
    }

    /// <summary>생성 참조(ReferenceNotes)를 비웁니다.</summary>
    [RelayCommand]
    private void ClearReferenceNotes()
    {
        Project.ReferenceNotes = string.Empty;
        StatusMessage = "생성 참조를 비웠습니다.";
    }

    /// <summary>작품 설계 데이터입니다. (파일 열기로 교체 가능)</summary>
    [ObservableProperty]
    private StoryProject _project;

    /// <summary>장르 샘플 목록입니다. (선택 또는 직접 입력)</summary>
    public IReadOnlyList<string> GenreSamples { get; } = new[]
    {
        "SF", "판타지", "로맨스", "로맨스 판타지", "스릴러", "미스터리", "추리",
        "호러", "무협", "역사", "드라마", "액션", "성장물", "코미디", "느와르", "디스토피아"
    };

    /// <summary>시대 샘플 목록입니다.</summary>
    public IReadOnlyList<string> EraSamples { get; } = new[]
    {
        "고대", "중세", "근대", "근현대", "현대", "근미래", "22세기", "먼 미래",
        "조선시대", "일제강점기", "가상의 시대", "종말 이후"
    };

    /// <summary>세계관 샘플 목록입니다.</summary>
    public IReadOnlyList<string> WorldSamples { get; } = new[]
    {
        "현실 기반", "하이 판타지", "로우 판타지", "우주 SF", "사이버펑크",
        "포스트 아포칼립스", "스팀펑크", "이세계", "무협 세계", "마법 학원",
        "신화 기반", "좀비 아포칼립스", "디스토피아", "평행세계"
    };

    /// <summary>결말 샘플 목록입니다.</summary>
    public IReadOnlyList<string> EndingSamples { get; } = new[]
    {
        "해피엔딩", "새드엔딩", "열린 결말", "반전 결말", "비극",
        "배드엔딩", "새로운 시작", "순환 결말", "희생으로 마무리", "모두 죽음"
    };

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _consistencyResult = string.Empty;

    /// <summary>본문 작성 시 대사 비율입니다. (0=묘사만, 100=대사만)</summary>
    [ObservableProperty]
    private int _dialogueRatio = 50;

    [ObservableProperty]
    private ChapterNode? _selectedChapter;

    [ObservableProperty]
    private SceneNode? _selectedScene;

    /// <summary>선택된 장이 있는지 여부입니다.</summary>
    public bool HasSelectedChapter => SelectedChapter is not null;

    /// <summary>선택된 Scene이 있는지 여부입니다.</summary>
    public bool HasSelectedScene => SelectedScene is not null;

    partial void OnSelectedChapterChanged(ChapterNode? value) => OnPropertyChanged(nameof(HasSelectedChapter));

    partial void OnSelectedSceneChanged(SceneNode? value) => OnPropertyChanged(nameof(HasSelectedScene));

    /// <summary>
    /// 트리에서 선택된 항목을 반영합니다. (뷰 코드비하인드에서 호출)
    /// </summary>
    public void Select(object? node)
    {
        switch (node)
        {
            case ChapterNode chapter:
                SelectedChapter = chapter;
                SelectedScene = null;
                break;
            case SceneNode scene:
                // Scene 편집 패널은 부모 장 패널 안에 있으므로, 부모 장도 함께 선택해야 바로 표시됩니다.
                SelectedChapter = Project.Chapters.FirstOrDefault(c => c.Scenes.Contains(scene)) ?? SelectedChapter;
                SelectedScene = scene;
                break;
        }
    }

    // ── 등장인물 ──

    [RelayCommand]
    private void AddCharacter() => Project.Characters.Add(new StoryCharacter { Name = "새 인물" });

    [RelayCommand]
    private void RemoveCharacter(StoryCharacter? character)
    {
        if (character is not null)
        {
            Project.Characters.Remove(character);
        }
    }

    /// <summary>
    /// 등장인물을 참고자료 폴더의 Characters 하위 폴더에 .md로 내보냅니다.
    /// </summary>
    [RelayCommand]
    private async Task ExportCharacterAsync(StoryCharacter? character)
    {
        if (character is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_referenceFolder) || !Directory.Exists(_referenceFolder))
        {
            StatusMessage = "참고자료 폴더가 없습니다. 참고자료 서랍에서 폴더를 먼저 지정하세요.";
            return;
        }

        var directory = Path.Combine(_referenceFolder, "Characters");
        Directory.CreateDirectory(directory);

        var safeName = MakeSafeFileName(string.IsNullOrWhiteSpace(character.Name) ? "인물" : character.Name);
        var path = Path.Combine(directory, safeName + ".md");
        var markdown =
            $"# {character.Name}\n\n"
            + $"- **성격**: {character.Personality}\n"
            + $"- **목표**: {character.Goal}\n"
            + $"- **비밀**: {character.Secret}\n"
            + $"- **관계**: {character.Relationships}\n";

        await File.WriteAllTextAsync(path, markdown);
        LoadReferences();
        StatusMessage = $"'{character.Name}'을(를) 참고자료로 내보냈습니다: Characters/{safeName}.md";
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }

    // ── 원고 역분석 (원고 → 설계) ──

    /// <summary>
    /// 원고를 분석해 작품 설정과 등장인물을 추출합니다.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeSettingsAsync()
    {
        var text = RequireManuscript();
        if (text is null || !ConfirmIf(HasSettingsData, "작품 설정과 등장인물"))
        {
            return;
        }

        await RunAsync("원고에서 작품 설정·등장인물을 추출하는 중...", async () =>
        {
            var condensed = await _plannerService.CondenseAsync(text, ProgressReporter());
            ApplySettings(await _plannerService.ExtractSettingsAsync(condensed));
        });
    }

    /// <summary>
    /// 원고에서 전체 시놉시스를 추출합니다.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeSynopsisAsync()
    {
        var text = RequireManuscript();
        if (text is null || !ConfirmIf(() => !string.IsNullOrWhiteSpace(Project.Synopsis), "시놉시스"))
        {
            return;
        }

        await RunAsync("원고에서 시놉시스를 추출하는 중...", async () =>
        {
            var condensed = await _plannerService.CondenseAsync(text, ProgressReporter());
            var synopsis = await _plannerService.ExtractSynopsisAsync(condensed);
            if (!string.IsNullOrWhiteSpace(synopsis))
            {
                Project.Synopsis = synopsis;
            }
        });
    }

    /// <summary>
    /// 원고를 분석해 장 구조를 추출합니다.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeChaptersAsync()
    {
        var text = RequireManuscript();
        if (text is null || !ConfirmIf(() => Project.Chapters.Count > 0, "장/Scene 구조"))
        {
            return;
        }

        await RunAsync("원고에서 장 구조를 추출하는 중...", async () =>
        {
            var condensed = await _plannerService.CondenseAsync(text, ProgressReporter());
            ApplyChapters(await _plannerService.ExtractChaptersAsync(condensed));
        });
    }

    /// <summary>
    /// 원고를 순차 분석해 설정·인물 → 시놉시스 → 장 → Scene까지 한 번에 만듭니다.
    /// </summary>
    [RelayCommand]
    private async Task AnalyzeAllAsync()
    {
        var text = RequireManuscript();
        if (text is null
            || !ConfirmIf(() => HasSettingsData() || !string.IsNullOrWhiteSpace(Project.Synopsis) || Project.Chapters.Count > 0,
                "전체 설계(설정·인물·시놉시스·장·Scene)"))
        {
            return;
        }

        await RunAsync("원고 전체를 분석하는 중... (설정 → 시놉시스 → 장 → Scene)", async () =>
        {
            // 장편 대응: 긴 원고는 한 번만 압축해 이후 단계에서 재사용합니다.
            var condensed = await _plannerService.CondenseAsync(text, ProgressReporter());

            StatusMessage = "작품 설정·등장인물을 추출하는 중...";
            ApplySettings(await _plannerService.ExtractSettingsAsync(condensed));

            StatusMessage = "시놉시스를 추출하는 중...";
            var synopsis = await _plannerService.ExtractSynopsisAsync(condensed);
            if (!string.IsNullOrWhiteSpace(synopsis))
            {
                Project.Synopsis = synopsis;
            }

            StatusMessage = "장 구조를 추출하는 중...";
            ApplyChapters(await _plannerService.ExtractChaptersAsync(condensed));

            // 각 장을 설정·시놉시스 기반으로 Scene까지 분할합니다.
            for (var i = 0; i < Project.Chapters.Count; i++)
            {
                StatusMessage = $"Scene 생성 중... ({i + 1}/{Project.Chapters.Count}장)";
                var chapter = Project.Chapters[i];
                var scenes = await _plannerService.GenerateScenesAsync(Project, chapter);
                if (scenes.Count > 0)
                {
                    chapter.Scenes.Clear();
                    foreach (var scene in scenes)
                    {
                        chapter.Scenes.Add(scene);
                    }
                }
            }
        });
    }

    private void ApplySettings(ExtractedSettings? settings)
    {
        if (settings is null)
        {
            return;
        }

        Project.Genre = settings.Genre;
        Project.Era = settings.Era;
        Project.World = settings.World;
        Project.CoreEvent = settings.CoreEvent;
        Project.Ending = settings.Ending;

        Project.Characters.Clear();
        foreach (var character in settings.Characters)
        {
            Project.Characters.Add(character);
        }
    }

    private void ApplyChapters(List<ChapterNode> chapters)
    {
        if (chapters.Count == 0)
        {
            return;
        }

        Project.Chapters.Clear();
        foreach (var chapter in chapters)
        {
            Project.Chapters.Add(chapter);
        }
    }

    private bool HasSettingsData()
    {
        return Project.Characters.Count > 0
            || !string.IsNullOrWhiteSpace(Project.Genre)
            || !string.IsNullOrWhiteSpace(Project.World)
            || !string.IsNullOrWhiteSpace(Project.CoreEvent);
    }

    private bool ConfirmIf(Func<bool> hasData, string what)
    {
        if (!hasData())
        {
            return true;
        }

        return ConfirmOverwrite?.Invoke($"기존 {what}을(를) 덮어씁니다. 계속할까요?") ?? true;
    }

    private IProgress<string> ProgressReporter() => new Progress<string>(message => StatusMessage = message);

    private string? RequireManuscript()
    {
        var text = GetManuscript?.Invoke();
        if (string.IsNullOrWhiteSpace(text))
        {
            StatusMessage = "분석할 원고가 없습니다. 에디터에 소설을 먼저 불러오세요.";
            return null;
        }

        return text;
    }

    // ── 시놉시스 / 장 / Scene 생성 ──

    [RelayCommand]
    private async Task GenerateSynopsisAsync()
    {
        await RunAsync("전체 시놉시스를 생성하는 중...", async () =>
        {
            var result = await _plannerService.GenerateSynopsisAsync(Project);
            if (!string.IsNullOrWhiteSpace(result))
            {
                Project.Synopsis = result;
            }
        });
    }

    [RelayCommand]
    private async Task GenerateChaptersAsync()
    {
        await RunAsync("장 구성을 생성하는 중...", async () =>
        {
            var chapters = await _plannerService.GenerateChaptersAsync(Project);
            if (chapters.Count > 0)
            {
                Project.Chapters.Clear();
                foreach (var chapter in chapters)
                {
                    Project.Chapters.Add(chapter);
                }
            }
        });
    }

    [RelayCommand]
    private async Task GenerateScenesAsync()
    {
        if (SelectedChapter is null)
        {
            return;
        }

        var chapter = SelectedChapter;
        await RunAsync($"'{chapter.Title}' 장을 Scene으로 분할하는 중...", async () =>
        {
            var scenes = await _plannerService.GenerateScenesAsync(Project, chapter);
            if (scenes.Count > 0)
            {
                chapter.Scenes.Clear();
                foreach (var scene in scenes)
                {
                    chapter.Scenes.Add(scene);
                }
            }
        });
    }

    [RelayCommand]
    private async Task GenerateSceneContentAsync()
    {
        if (SelectedChapter is null || SelectedScene is null)
        {
            return;
        }

        var chapter = SelectedChapter;
        var scene = SelectedScene;
        var previous = PreviousSceneSummary(chapter, scene);

        await RunAsync($"'{scene.Title}' Scene 본문을 작성하는 중...", async () =>
        {
            var content = await _plannerService.GenerateSceneContentAsync(Project, chapter, scene, previous, DialogueRatio);
            if (!string.IsNullOrWhiteSpace(content))
            {
                scene.Content = content;
            }
        });
    }

    /// <summary>
    /// 선택 Scene을 비트로 나눠 상세하고 긴 본문을 작성합니다.
    /// </summary>
    [RelayCommand]
    private async Task GenerateSceneContentDetailedAsync()
    {
        if (SelectedChapter is null || SelectedScene is null)
        {
            return;
        }

        var chapter = SelectedChapter;
        var scene = SelectedScene;
        var previous = PreviousSceneSummary(chapter, scene);

        await RunAsync($"'{scene.Title}' Scene 상세 본문을 작성하는 중...", async () =>
        {
            var content = await _plannerService.GenerateSceneContentDetailedAsync(
                Project, chapter, scene, previous, DialogueRatio, ProgressReporter());
            if (!string.IsNullOrWhiteSpace(content))
            {
                scene.Content = content;
            }
        });
    }

    [RelayCommand]
    private async Task CheckConsistencyAsync()
    {
        await RunAsync("스토리 일관성을 검사하는 중...", async () =>
        {
            var result = await _plannerService.CheckConsistencyAsync(Project);
            ConsistencyResult = string.IsNullOrWhiteSpace(result) ? "결과를 받지 못했습니다." : result;
        });
    }

    // ── 장/Scene 편집 ──

    [RelayCommand]
    private void AddChapter()
    {
        var chapter = new ChapterNode { Title = $"{Project.Chapters.Count + 1}장" };
        Project.Chapters.Add(chapter);
        SelectedChapter = chapter;
    }

    [RelayCommand]
    private void RemoveChapter()
    {
        if (SelectedChapter is not null)
        {
            Project.Chapters.Remove(SelectedChapter);
            SelectedChapter = null;
        }
    }

    [RelayCommand]
    private void AddScene()
    {
        if (SelectedChapter is null)
        {
            return;
        }

        var scene = new SceneNode { Title = $"Scene {SelectedChapter.Scenes.Count + 1}" };
        SelectedChapter.Scenes.Add(scene);
        SelectedScene = scene;
    }

    [RelayCommand]
    private void RemoveScene()
    {
        if (SelectedChapter is not null && SelectedScene is not null)
        {
            SelectedChapter.Scenes.Remove(SelectedScene);
            SelectedScene = null;
        }
    }

    /// <summary>
    /// 선택한 Scene 본문을 메인 에디터에 삽입합니다.
    /// </summary>
    [RelayCommand]
    private void InsertSceneToEditor()
    {
        if (SelectedScene is not null && !string.IsNullOrWhiteSpace(SelectedScene.Content))
        {
            InsertToEditor?.Invoke(SelectedScene.Content);
            StatusMessage = "본문을 에디터에 삽입했습니다.";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _projectService.SaveAsync(Project);
        StatusMessage = "작품 설계를 저장했습니다.";
    }

    /// <summary>
    /// 작품 설계를 파일로 저장합니다. (다른 이름으로 저장)
    /// </summary>
    [RelayCommand]
    private async Task SaveToFileAsync()
    {
        if (SaveAsPathResolver is null)
        {
            return;
        }

        var path = await SaveAsPathResolver();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _projectService.SaveToPathAsync(Project, path);
        StatusMessage = $"파일로 저장했습니다: {System.IO.Path.GetFileName(path)}";
    }

    /// <summary>
    /// 파일에서 작품 설계를 불러옵니다.
    /// </summary>
    [RelayCommand]
    private async Task OpenFromFileAsync()
    {
        if (OpenPathResolver is null)
        {
            return;
        }

        var path = await OpenPathResolver();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var loaded = _projectService.LoadFromPath(path);
        if (loaded is null)
        {
            StatusMessage = "파일을 불러오지 못했습니다.";
            return;
        }

        SelectedScene = null;
        SelectedChapter = null;
        Project = loaded;
        StatusMessage = $"파일을 불러왔습니다: {System.IO.Path.GetFileName(path)}";
    }

    private static string PreviousSceneSummary(ChapterNode chapter, SceneNode scene)
    {
        var index = chapter.Scenes.IndexOf(scene);
        return index > 0 ? chapter.Scenes[index - 1].Summary : string.Empty;
    }

    private async Task RunAsync(string status, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = status;
        try
        {
            await action();
            StatusMessage = "완료되었습니다.";
        }
        catch (Exception ex)
        {
            StatusMessage = "오류: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
