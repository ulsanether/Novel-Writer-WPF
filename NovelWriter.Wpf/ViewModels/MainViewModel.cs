using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelWriter.Wpf.Models;
using NovelWriter.Wpf.Services;

namespace NovelWriter.Wpf.ViewModels;

/// <summary>
/// 메인 에디터 화면 ViewModel입니다.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly DocumentRepository _repository;
    private readonly BackupService _backupService;
    private readonly DocumentImportService _documentImportService;
    private readonly DocxExportService _docxExportService;
    private readonly TypoCorrectionService _typoCorrectionService;
    private readonly OllamaService _ollamaService;
    private readonly ChatService _chatService;
    private readonly ImageServiceRouter _imageService;
    private readonly ImageSetupService _imageSetupService;
    private readonly ComfyUiSetupService _comfyUiSetupService;
    private readonly HunspellSpellCheckService _hunspellService;
    private readonly UserDictionaryService _userDictionaryService;
    private readonly ReferenceLibraryService _referenceLibraryService;
    private readonly StatisticsService _statisticsService;
    private readonly LocalizationService _localizationService;
    private readonly ThemeService _themeService;
    private readonly SettingsService _settingsService;
    private readonly NovelProjectService _novelProjectService;
    private readonly DispatcherTimer _autoSaveTimer;
    private CancellationTokenSource? _pullCancellation;
    private bool _suppressMarkClear;
    private readonly DispatcherTimer _spellCheckTimer;
    private readonly HashSet<string> _ignoredWords = new(StringComparer.Ordinal);
    private static readonly Regex HangulWordRegex = new("[가-힣]+", RegexOptions.Compiled);

    /// <summary>
    /// 오타로 표시된 구간 목록입니다. (뷰의 물결선 어도너가 구독합니다)
    /// </summary>
    public ObservableCollection<TypoMark> TypoMarks { get; } = new();

    /// <summary>
    /// 현재 화면에 보이는 텍스트 범위(시작 인덱스, 길이)를 반환하는 콜백입니다. null이면 전체를 검사합니다.
    /// </summary>
    public Func<(int Start, int Length)?>? VisibleRangeResolver { get; set; }

    /// <summary>
    /// 서랍에 표시되는 참고자료 목록입니다.
    /// </summary>
    public ObservableCollection<ReferenceDocument> References { get; } = new();

    /// <summary>
    /// 참고자료 폴더 선택 UI를 호출하는 콜백입니다.
    /// </summary>
    public Func<Task<string?>>? ReferenceFolderResolver { get; set; }

    private string _referenceFolder = string.Empty;

    /// <summary>참고자료 폴더 경로입니다. (스토리 플래너/생성기 연동용)</summary>
    public string ReferenceFolderPath => _referenceFolder;

    // ── 작품 프로젝트(.novel) 통합 관리 ──

    /// <summary>현재 열려 있는 작품 프로젝트입니다. (없으면 null)</summary>
    public NovelProject? CurrentProject { get; private set; }

    private bool _suppressDirty;

    /// <summary>편집 후 작품(.novel)에 저장되지 않은 변경이 있는지 여부입니다.</summary>
    public bool IsProjectDirty { get; private set; }

    /// <summary>편집 발생을 표시합니다. (닫기 전 저장 확인용)</summary>
    public void MarkProjectDirty()
    {
        if (!_suppressDirty)
        {
            IsProjectDirty = true;
        }
    }

    /// <summary>창 제목입니다. (작품명 포함)</summary>
    public string AppTitle => string.IsNullOrWhiteSpace(_novelProjectService.CurrentPath)
        ? "글만들기"
        : $"{Title} — 글만들기";

    /// <summary>현재 프로젝트가 열려 있는지 여부입니다.</summary>
    public bool HasProject => CurrentProject is not null;

    /// <summary>새 작품 생성 시 상위 폴더·제목을 받는 콜백입니다.</summary>
    public Func<Task<(string ParentFolder, string Title)?>>? NewProjectResolver { get; set; }

    /// <summary>작품 파일(.novel) 열기 경로 콜백입니다.</summary>
    public Func<Task<string?>>? OpenProjectResolver { get; set; }

    /// <summary>작품 다른 이름으로 저장 경로 콜백입니다. (제안 파일명)</summary>
    public Func<string, Task<string?>>? SaveProjectAsResolver { get; set; }

    /// <summary>스토리 플래너에 현재 작품의 설계를 넘겨주기 위한 알림입니다. (View 재오픈용)</summary>
    public event Action? ProjectChanged;

    /// <summary>
    /// 새 작품을 만듭니다. (폴더+하위폴더+.novel 생성 후 현재 상태로 적용)
    /// </summary>
    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (NewProjectResolver is null)
        {
            return;
        }

        var choice = await NewProjectResolver();
        if (choice is null || string.IsNullOrWhiteSpace(choice.Value.ParentFolder))
        {
            return;
        }

        // 새 작품은 현재 AI/이미지 설정을 초기값으로 물려받습니다.
        var (project, _) = await _novelProjectService.CreateAsync(choice.Value.ParentFolder, choice.Value.Title);
        project.Manuscript = string.Empty;
        project.Ai = new ProjectAiSettings { Model = AiModel, BaseUrl = _aiBaseUrl };
        project.Image = new ProjectImageSettings
        {
            ComfyUiBaseUrl = ComfyUiBaseUrl,
            ComfyUiPath = ComfyUiPath,
            Checkpoint = _imageService.Comfy.CheckpointName,
            Hardware = (SelectedHardware ?? HardwareProfiles[0]).Key,
            Style = CurrentStyle()
        };
        await _novelProjectService.SaveAsync(project, _novelProjectService.CurrentPath!);

        ApplyProject(project);
        StatusMessage = $"새 작품을 만들었습니다: {project.Title}";
    }

    /// <summary>
    /// 작품 파일(.novel)을 엽니다.
    /// </summary>
    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (OpenProjectResolver is null)
        {
            return;
        }

        var path = await OpenProjectResolver();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var project = _novelProjectService.Load(path);
        if (project is null)
        {
            StatusMessage = "작품 파일을 열지 못했습니다.";
            return;
        }

        ApplyProject(project);
        StatusMessage = $"작품을 열었습니다: {project.Title}";
    }

    /// <summary>
    /// 현재 작품을 저장합니다. (열린 프로젝트가 없으면 저장 경로를 물어봅니다)
    /// </summary>
    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        var project = CurrentProject;
        if (project is null)
        {
            // 프로젝트가 없으면 새로 만들되, 현재 원고를 잃지 않도록 보존합니다.
            if (NewProjectResolver is null)
            {
                return;
            }

            var savedContent = Content;
            await NewProjectAsync();
            if (CurrentProject is null)
            {
                return;
            }

            project = CurrentProject;
            Content = savedContent; // 새 작품 생성이 편집기를 비웠으므로 현재 원고 복원
        }

        // 현재 편집 상태를 프로젝트에 반영
        project.Title = Title;
        project.Manuscript = Content ?? string.Empty;
        project.Ai = new ProjectAiSettings { Model = AiModel, BaseUrl = _aiBaseUrl };
        project.Image = new ProjectImageSettings
        {
            ComfyUiBaseUrl = ComfyUiBaseUrl,
            ComfyUiPath = ComfyUiPath,
            Checkpoint = _imageService.Comfy.CheckpointName,
            Hardware = (SelectedHardware ?? HardwareProfiles[0]).Key,
            Style = CurrentStyle()
        };
        // Story는 스토리 플래너가 같은 인스턴스를 편집하므로 CurrentProject.Story에 이미 반영되어 있습니다.

        await _novelProjectService.SaveAsync(project, _novelProjectService.CurrentPath!);
        IsProjectDirty = false;
        OnPropertyChanged(nameof(AppTitle));
        StatusMessage = $"작품을 저장했습니다: {Path.GetFileName(_novelProjectService.CurrentPath)}";
    }

    /// <summary>
    /// 프로젝트 내용을 현재 편집 상태에 적용합니다. (원고·AI·이미지·참고자료 폴더)
    /// </summary>
    private void ApplyProject(NovelProject project)
    {
        _suppressDirty = true;
        CurrentProject = project;

        Title = string.IsNullOrWhiteSpace(project.Title) ? "새 작품" : project.Title;
        Content = project.Manuscript ?? string.Empty;

        // 작품별 AI 설정 (비어 있으면 기존값 유지)
        if (!string.IsNullOrWhiteSpace(project.Ai?.Model))
        {
            AiModel = project.Ai.Model;
        }

        if (!string.IsNullOrWhiteSpace(project.Ai?.BaseUrl))
        {
            _aiBaseUrl = project.Ai.BaseUrl;
        }

        _typoCorrectionService.Model = AiModel;
        _typoCorrectionService.BaseUrl = _aiBaseUrl;
        _chatService.Model = AiModel;
        _chatService.BaseUrl = _aiBaseUrl;
        _ollamaService.BaseUrl = ToOllamaNativeUrl(_aiBaseUrl);

        // 작품별 이미지 설정
        if (project.Image is not null)
        {
            if (!string.IsNullOrWhiteSpace(project.Image.ComfyUiBaseUrl))
            {
                ComfyUiBaseUrl = project.Image.ComfyUiBaseUrl;
            }

            if (!string.IsNullOrWhiteSpace(project.Image.ComfyUiPath))
            {
                ComfyUiPath = project.Image.ComfyUiPath;
            }

            _imageService.Comfy.CheckpointName = project.Image.Checkpoint ?? _imageService.Comfy.CheckpointName;
            var hw = HardwareProfiles.FirstOrDefault(h => string.Equals(h.Key, project.Image.Hardware, StringComparison.OrdinalIgnoreCase));
            if (hw is not null)
            {
                SelectedHardware = hw;
            }

            _imageService.Comfy.BaseUrl = ComfyUiBaseUrl;
            LoadStyle(project.Image.Style);
        }

        // 참고자료·이미지 폴더를 작품 폴더로 지정 (한 폴더에서 통합 관리)
        var folder = _novelProjectService.CurrentFolder;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            _referenceFolder = folder;
            OnPropertyChanged(nameof(ReferenceFolderPath));
            LoadReferences();
        }

        TypoMarks.Clear();
        UpdateStatistics();
        OnPropertyChanged(nameof(CurrentProject));
        OnPropertyChanged(nameof(HasProject));
        OnPropertyChanged(nameof(AppTitle));
        ProjectChanged?.Invoke();

        _suppressDirty = false;
        IsProjectDirty = false;
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(AppTitle));
        MarkProjectDirty();
    }

    // ── 이미지 생성 서버 설정/설치 ──

    /// <summary>ComfyUI 백엔드를 사용하는지 여부입니다. (false면 A1111)</summary>
    [ObservableProperty]
    private bool _useComfyUi;

    [ObservableProperty]
    private string _imageBaseUrl = "http://127.0.0.1:7860";

    [ObservableProperty]
    private string _imageWebUiPath = string.Empty;

    [ObservableProperty]
    private string _comfyUiBaseUrl = "http://127.0.0.1:8188";

    [ObservableProperty]
    private string _comfyUiPath = string.Empty;

    /// <summary>ComfyUI 추천 모델 목록입니다.</summary>
    public IReadOnlyList<ComfyUiSetupService.ComfyModel> ComfyModels => ComfyUiSetupService.RecommendedModels;

    /// <summary>다운로드할 ComfyUI 모델 선택입니다.</summary>
    [ObservableProperty]
    private ComfyUiSetupService.ComfyModel? _selectedComfyModel = ComfyUiSetupService.RecommendedModels[0];

    /// <summary>하드웨어(VRAM) 프로파일 목록입니다.</summary>
    public IReadOnlyList<HardwareProfile> HardwareProfiles { get; } = new[]
    {
        new HardwareProfile("Auto", "자동 (권장)", "", "", "VRAM에 맞춰 자동 관리합니다."),
        new HardwareProfile("High", "고사양 · 12GB 이상", "--highvram", "", "최대 속도로 실행합니다."),
        new HardwareProfile("Medium", "중간 · 8GB", "--normalvram", "--medvram", "균형(8GB 권장)."),
        new HardwareProfile("Low", "저사양 · 6GB 이하", "--lowvram", "--lowvram", "VRAM을 아끼지만 느립니다."),
        new HardwareProfile("Cpu", "GPU 없음 (CPU만)", "--cpu", "--use-cpu all --skip-torch-cuda-test", "그래픽카드 없이 실행(매우 느림).")
    };

    /// <summary>선택한 하드웨어 프로파일입니다.</summary>
    [ObservableProperty]
    private HardwareProfile _selectedHardware;

    // ── 화풍(이미지 스타일) 설정 ──

    /// <summary>화풍 프리셋 목록입니다.</summary>
    public IReadOnlyList<string> StylePresetOptions => ImageStyleCatalog.PresetLabels;

    /// <summary>품질 목록입니다.</summary>
    public IReadOnlyList<string> StyleQualityOptions => ImageStyleCatalog.QualityLabels;

    /// <summary>조명 목록입니다.</summary>
    public IReadOnlyList<string> StyleLightingOptions => ImageStyleCatalog.LightingLabels;

    /// <summary>색감 목록입니다.</summary>
    public IReadOnlyList<string> StyleColorMoodOptions => ImageStyleCatalog.ColorMoodLabels;

    [ObservableProperty]
    private string _stylePreset = "스토리북";

    [ObservableProperty]
    private string _styleQuality = "고품질";

    [ObservableProperty]
    private string _styleLighting = "없음";

    [ObservableProperty]
    private string _styleColorMood = "없음";

    [ObservableProperty]
    private string _styleExtraPositive = string.Empty;

    [ObservableProperty]
    private string _styleExtraNegative = string.Empty;

    /// <summary>촬영 범위 목록입니다.</summary>
    public IReadOnlyList<string> StyleShotOptions => ImageStyleCatalog.ShotLabels;

    /// <summary>카메라 각도 목록입니다.</summary>
    public IReadOnlyList<string> StyleCameraAngleOptions => ImageStyleCatalog.CameraAngleLabels;

    /// <summary>분위기 목록입니다.</summary>
    public IReadOnlyList<string> StyleMoodOptions => ImageStyleCatalog.MoodLabels;

    /// <summary>배경 목록입니다.</summary>
    public IReadOnlyList<string> StyleBackgroundOptions => ImageStyleCatalog.BackgroundLabels;

    /// <summary>시간대 목록입니다.</summary>
    public IReadOnlyList<string> StyleTimeOfDayOptions => ImageStyleCatalog.TimeOfDayLabels;

    /// <summary>콘텐츠 이용 등급 목록입니다.</summary>
    public IReadOnlyList<string> StyleContentRatingOptions => ImageStyleCatalog.ContentRatingLabels;

    [ObservableProperty]
    private string _styleShot = "자동";

    [ObservableProperty]
    private string _styleCameraAngle = "자동";

    [ObservableProperty]
    private string _styleMood = "없음";

    [ObservableProperty]
    private string _styleBackground = "자동";

    [ObservableProperty]
    private string _styleTimeOfDay = "자동";

    [ObservableProperty]
    private string _styleContentRating = "전체 이용가";

    [ObservableProperty]
    private int _styleRealism = 50;

    [ObservableProperty]
    private int _styleDetail = 60;

    [ObservableProperty]
    private int _styleBackgroundComplexity = 50;

    partial void OnStyleShotChanged(string value) => ApplyImageStyle();

    partial void OnStyleCameraAngleChanged(string value) => ApplyImageStyle();

    partial void OnStyleMoodChanged(string value) => ApplyImageStyle();

    partial void OnStyleBackgroundChanged(string value) => ApplyImageStyle();

    partial void OnStyleTimeOfDayChanged(string value) => ApplyImageStyle();

    partial void OnStyleContentRatingChanged(string value) => ApplyImageStyle();

    partial void OnStyleRealismChanged(int value) => ApplyImageStyle();

    partial void OnStyleDetailChanged(int value) => ApplyImageStyle();

    partial void OnStyleBackgroundComplexityChanged(int value) => ApplyImageStyle();

    /// <summary>현재 스타일에서 만들어진 긍정 접두입니다. (스토리 플래너/참고자료 생성기 공용)</summary>
    public string CurrentStylePrefix { get; private set; } = ImageStyleCatalog.BuildPositivePrefix(new ImageStyleSettings());

    /// <summary>스타일 긍정 프롬프트 미리보기입니다.</summary>
    public string StylePositivePreview => CurrentStylePrefix;

    /// <summary>스타일 부정 프롬프트 미리보기입니다.</summary>
    public string StyleNegativePreview => _imageService.Comfy.NegativePrompt;

    /// <summary>프리셋이 적용한 해상도/스텝 미리보기입니다.</summary>
    public string StyleResolutionPreview => $"{_imageService.Comfy.Width}×{_imageService.Comfy.Height} · {_imageService.Comfy.Steps} steps";

    partial void OnStylePresetChanged(string value) => ApplyImageStyle();

    partial void OnStyleQualityChanged(string value) => ApplyImageStyle();

    partial void OnStyleLightingChanged(string value) => ApplyImageStyle();

    partial void OnStyleColorMoodChanged(string value) => ApplyImageStyle();

    partial void OnStyleExtraPositiveChanged(string value) => ApplyImageStyle();

    partial void OnStyleExtraNegativeChanged(string value) => ApplyImageStyle();

    /// <summary>현재 UI 값으로 스타일 설정 객체를 만듭니다.</summary>
    private ImageStyleSettings CurrentStyle() => new()
    {
        Preset = StylePreset,
        Quality = StyleQuality,
        Lighting = StyleLighting,
        ColorMood = StyleColorMood,
        ExtraPositive = StyleExtraPositive,
        ExtraNegative = StyleExtraNegative,
        Shot = StyleShot,
        CameraAngle = StyleCameraAngle,
        Mood = StyleMood,
        Background = StyleBackground,
        TimeOfDay = StyleTimeOfDay,
        ContentRating = StyleContentRating,
        Realism = StyleRealism,
        Detail = StyleDetail,
        BackgroundComplexity = StyleBackgroundComplexity
    };

    /// <summary>
    /// 현재 화풍 설정을 이미지 백엔드와 스토리 설계에 적용합니다.
    /// </summary>
    private void ApplyImageStyle()
    {
        var style = CurrentStyle();
        CurrentStylePrefix = ImageStyleCatalog.BuildPositivePrefix(style);
        var negative = ImageStyleCatalog.BuildNegative(style);
        _imageService.Comfy.NegativePrompt = negative;
        _imageService.A1111.NegativePrompt = negative;

        // 프리셋 추천 해상도/스텝 적용 (해상도는 항상, 스텝은 저스텝 특수 모델 FLUX/Turbo 보호)
        var preset = ImageStyleCatalog.FindPreset(style.Preset);
        _imageService.Comfy.Width = preset.Width;
        _imageService.Comfy.Height = preset.Height;
        if (_imageService.Comfy.Steps >= 10)
        {
            _imageService.Comfy.Steps = preset.Steps;
        }

        // 스토리 플래너는 Project.ImageStylePrefix를 사용하므로 함께 반영
        if (CurrentProject?.Story is not null)
        {
            CurrentProject.Story.ImageStylePrefix = CurrentStylePrefix;
        }

        OnPropertyChanged(nameof(StylePositivePreview));
        OnPropertyChanged(nameof(StyleNegativePreview));
        OnPropertyChanged(nameof(StyleResolutionPreview));
        MarkProjectDirty();
    }

    /// <summary>스타일 설정 객체 값을 UI 속성에 반영합니다. (적용은 각 OnChanged가 담당)</summary>
    private void LoadStyle(ImageStyleSettings? style)
    {
        if (style is null)
        {
            return;
        }

        var prevSuppress = _suppressDirty;
        _suppressDirty = true;
        StylePreset = string.IsNullOrWhiteSpace(style.Preset) ? "스토리북" : style.Preset;
        StyleQuality = string.IsNullOrWhiteSpace(style.Quality) ? "고품질" : style.Quality;
        StyleLighting = string.IsNullOrWhiteSpace(style.Lighting) ? "없음" : style.Lighting;
        StyleColorMood = string.IsNullOrWhiteSpace(style.ColorMood) ? "없음" : style.ColorMood;
        StyleExtraPositive = style.ExtraPositive ?? string.Empty;
        StyleExtraNegative = style.ExtraNegative ?? string.Empty;
        StyleShot = string.IsNullOrWhiteSpace(style.Shot) ? "자동" : style.Shot;
        StyleCameraAngle = string.IsNullOrWhiteSpace(style.CameraAngle) ? "자동" : style.CameraAngle;
        StyleMood = string.IsNullOrWhiteSpace(style.Mood) ? "없음" : style.Mood;
        StyleBackground = string.IsNullOrWhiteSpace(style.Background) ? "자동" : style.Background;
        StyleTimeOfDay = string.IsNullOrWhiteSpace(style.TimeOfDay) ? "자동" : style.TimeOfDay;
        StyleContentRating = string.IsNullOrWhiteSpace(style.ContentRating) ? "전체 이용가" : style.ContentRating;
        StyleRealism = style.Realism;
        StyleDetail = style.Detail;
        StyleBackgroundComplexity = style.BackgroundComplexity;
        _suppressDirty = prevSuppress;
        ApplyImageStyle();
    }

    [ObservableProperty]
    private string _imageServerStatus = "확인되지 않음";

    [ObservableProperty]
    private string _imageSetupLog = string.Empty;

    [ObservableProperty]
    private bool _isImageSetupBusy;

    /// <summary>이미지 서버 설치 폴더 선택 콜백입니다.</summary>
    public Func<Task<string?>>? ImageInstallFolderResolver { get; set; }

    partial void OnImageBaseUrlChanged(string value) => _imageService.A1111.BaseUrl = value;

    partial void OnComfyUiBaseUrlChanged(string value) => _imageService.Comfy.BaseUrl = value;

    partial void OnUseComfyUiChanged(bool value)
        => _imageService.Backend = value ? ImageBackendKind.ComfyUi : ImageBackendKind.A1111;

    /// <summary>
    /// 현재 선택된 백엔드의 연결을 확인합니다.
    /// </summary>
    [RelayCommand]
    private async Task TestImageServerAsync()
    {
        _imageService.A1111.BaseUrl = ImageBaseUrl;
        _imageService.Comfy.BaseUrl = ComfyUiBaseUrl;
        _imageService.Backend = UseComfyUi ? ImageBackendKind.ComfyUi : ImageBackendKind.A1111;

        ImageServerStatus = "확인 중...";
        var running = await _imageService.IsRunningAsync();
        var name = UseComfyUi ? "ComfyUI" : "A1111";
        ImageServerStatus = running ? $"✅ {name} 실행 중 (연결됨)" : $"❌ {name} 응답 없음 (설치·실행 필요)";
    }

    /// <summary>
    /// A1111 SD WebUI를 자동으로 내려받아 설치합니다. (Python·git 자동 설치)
    /// </summary>
    [RelayCommand]
    private async Task InstallImageServerAsync()
    {
        if (ImageInstallFolderResolver is null || IsImageSetupBusy)
        {
            return;
        }

        var folder = await ImageInstallFolderResolver();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        IsImageSetupBusy = true;
        ImageSetupLog = string.Empty;
        var progress = new Progress<string>(line => ImageSetupLog += line + "\n");

        try
        {
            var ok = await _imageSetupService.InstallAsync(folder, progress);
            if (ok)
            {
                ImageWebUiPath = System.IO.Path.Combine(folder, "stable-diffusion-webui");
                await PersistSettingsAsync();
            }
        }
        finally
        {
            IsImageSetupBusy = false;
        }
    }

    /// <summary>
    /// ComfyUI 포터블을 자동으로 내려받아 설치합니다. (Python 불필요, 7-Zip 자동 설치)
    /// </summary>
    [RelayCommand]
    private async Task InstallComfyUiAsync()
    {
        if (ImageInstallFolderResolver is null || IsImageSetupBusy)
        {
            return;
        }

        var folder = await ImageInstallFolderResolver();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        IsImageSetupBusy = true;
        ImageSetupLog = string.Empty;
        var progress = new Progress<string>(line => ImageSetupLog += line + "\n");

        try
        {
            var ok = await _comfyUiSetupService.InstallAsync(folder, progress);
            if (ok)
            {
                ComfyUiPath = folder;
                UseComfyUi = true;
                await PersistSettingsAsync();
            }
        }
        finally
        {
            IsImageSetupBusy = false;
        }
    }

    /// <summary>
    /// 선택된 백엔드 서버를 실행합니다. (첫 실행 시 필요한 파일 자동 다운로드)
    /// </summary>
    [RelayCommand]
    private void LaunchImageServer()
    {
        var hw = SelectedHardware ?? HardwareProfiles[0];

        if (UseComfyUi)
        {
            if (!_comfyUiSetupService.IsInstalled(ComfyUiPath))
            {
                ImageSetupLog += "설치된 ComfyUI 경로가 없습니다. 먼저 설치하거나 폴더를 지정하세요.\n";
                return;
            }

            var okC = _comfyUiSetupService.Launch(ComfyUiPath, hw.ComfyArgs);
            ImageSetupLog += okC ? $"ComfyUI를 실행했습니다({hw.DisplayName}). 콘솔 창에서 준비가 끝나면 [연결 확인]을 눌러주세요.\n" : "실행에 실패했습니다.\n";
            return;
        }

        if (string.IsNullOrWhiteSpace(ImageWebUiPath) || !_imageSetupService.IsInstalled(ImageWebUiPath))
        {
            ImageSetupLog += "설치된 WebUI 경로가 없습니다. 먼저 설치하거나 폴더를 지정하세요.\n";
            return;
        }

        _imageSetupService.EnsureApiFlag(ImageWebUiPath);
        _imageSetupService.EnsurePipConstraints(ImageWebUiPath);
        _imageSetupService.EnsureExtraArgs(ImageWebUiPath, hw.A1111Args);
        var ok = _imageSetupService.Launch(ImageWebUiPath);
        ImageSetupLog += ok ? "WebUI를 실행했습니다. 콘솔 창에서 준비가 끝나면 [연결 확인]을 눌러주세요.\n" : "실행에 실패했습니다.\n";
    }

    /// <summary>
    /// ComfyUI 체크포인트(모델) 폴더를 탐색기로 엽니다.
    /// </summary>
    [RelayCommand]
    private void OpenComfyModelsFolder()
    {
        var path = _comfyUiSetupService.GetCheckpointsFolder(ComfyUiPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            ImageSetupLog += "먼저 ComfyUI를 설치하세요.\n";
            return;
        }

        try
        {
            System.IO.Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// 선택한 추천 모델을 ComfyUI 체크포인트 폴더로 내려받고, 그 모델의 권장 설정을 적용합니다.
    /// </summary>
    [RelayCommand]
    private async Task DownloadComfyModelAsync()
    {
        if (SelectedComfyModel is null || IsImageSetupBusy)
        {
            return;
        }

        if (!_comfyUiSetupService.IsInstalled(ComfyUiPath))
        {
            ImageSetupLog += "먼저 ComfyUI를 설치하세요.\n";
            return;
        }

        IsImageSetupBusy = true;
        ImageSetupLog = string.Empty;
        var progress = new Progress<string>(line => ImageSetupLog += line + "\n");
        var model = SelectedComfyModel;

        try
        {
            var ok = await _comfyUiSetupService.DownloadModelAsync(ComfyUiPath, model, progress);
            if (ok)
            {
                ApplyComfyModel(model);
                UseComfyUi = true;
                await PersistSettingsAsync();
            }
        }
        finally
        {
            IsImageSetupBusy = false;
        }
    }

    /// <summary>모델 자동 다운로드 전 사용자 확인 콜백입니다. (메시지 → true=진행)</summary>
    public Func<string, bool>? ConfirmImageModelDownload { get; set; }

    /// <summary>
    /// 이미지 모델이 준비됐는지 확인하고, 없으면 자동으로 내려받아 연결합니다.
    /// </summary>
    /// <returns>생성 가능한 모델이 준비되면 true.</returns>
    public async Task<bool> EnsureComfyModelReadyAsync()
    {
        if (!await _imageService.Comfy.IsRunningAsync())
        {
            ImageServerStatus = "이미지 서버 상태: ❌ 서버 미실행 — [서버 실행] 후 다시 시도";
            return false;
        }

        // 이미 설치된 모델이 있으면 그대로 사용
        if (await _imageService.Comfy.HasCheckpointAsync())
        {
            ImageServerStatus = $"이미지 서버 상태: ✅ 모델 준비됨 ({_imageService.Comfy.CheckpointName})";
            return true;
        }

        // 모델이 없으면 자동 다운로드 시도
        if (!_comfyUiSetupService.IsInstalled(ComfyUiPath))
        {
            ImageSetupLog += "설치 폴더를 알 수 없어 자동 다운로드할 수 없습니다. [모델 폴더 열기]로 .safetensors를 직접 넣어주세요.\n";
            ImageServerStatus = "이미지 서버 상태: ❌ 모델 없음 (수동 배치 필요)";
            return false;
        }

        var model = SelectedComfyModel ?? ComfyModels[0];
        if (ConfirmImageModelDownload is not null &&
            !ConfirmImageModelDownload($"설치된 이미지 모델이 없습니다.\n'{model.DisplayName}'을(를) 지금 자동으로 내려받을까요?\n({model.Note})"))
        {
            return false;
        }

        IsImageSetupBusy = true;
        var progress = new Progress<string>(line => ImageSetupLog += line + "\n");
        try
        {
            var ok = await _comfyUiSetupService.DownloadModelAsync(ComfyUiPath, model, progress);
            if (!ok)
            {
                ImageServerStatus = "이미지 서버 상태: ❌ 모델 다운로드 실패";
                return false;
            }

            ApplyComfyModel(model);
            await PersistSettingsAsync();

            var ready = await _imageService.Comfy.HasCheckpointAsync();
            ImageServerStatus = ready
                ? $"이미지 서버 상태: ✅ 모델 준비됨 ({model.FileName})"
                : "이미지 서버 상태: ⚠ 모델 받음 — ComfyUI를 재시작하면 인식됩니다";
            return ready;
        }
        finally
        {
            IsImageSetupBusy = false;
        }
    }

    /// <summary>
    /// 이미지 모델을 준비합니다. (버튼용 — 없으면 자동 다운로드)
    /// </summary>
    [RelayCommand]
    private async Task EnsureComfyModelAsync() => await EnsureComfyModelReadyAsync();

    // 모델의 파일명과 권장 샘플링 설정을 ComfyUI 백엔드에 적용합니다.
    private void ApplyComfyModel(ComfyUiSetupService.ComfyModel model)
    {
        var comfy = _imageService.Comfy;
        comfy.CheckpointName = model.FileName;
        comfy.Steps = model.Steps;
        comfy.CfgScale = model.Cfg;
        comfy.Sampler = model.Sampler;
        comfy.Scheduler = model.Scheduler;
        comfy.Width = model.Width;
        comfy.Height = model.Height;
    }

    /// <summary>
    /// 선택된 백엔드의 설치/모델 안내 페이지를 엽니다.
    /// </summary>
    [RelayCommand]
    private void OpenImageServerPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = UseComfyUi
                    ? "https://github.com/comfyanonymous/ComfyUI#installing"
                    : "https://github.com/AUTOMATIC1111/stable-diffusion-webui#installation-and-running",
                UseShellExecute = true
            });
        }
        catch
        {
            // 무시
        }
    }

    [ObservableProperty]
    private ReferenceDocument? _selectedReference;

    [ObservableProperty]
    private bool _isReferenceDrawerOpen;

    /// <summary>
    /// 참고자료 목록이 비어 있는지 여부입니다. (빈 상태 안내 표시용)
    /// </summary>
    public bool HasNoReferences => References.Count == 0;

    /// <summary>
    /// AI 채팅 대화 기록입니다.
    /// </summary>
    public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private bool _isChatDrawerOpen;

    [ObservableProperty]
    private bool _isChatBusy;

    /// <summary>
    /// 채팅 기록이 비어 있는지 여부입니다. (빈 상태 안내 표시용)
    /// </summary>
    public bool HasNoMessages => ChatMessages.Count == 0;

    /// <summary>
    /// 파일 열기 UI를 호출하기 위한 콜백입니다.
    /// </summary>
    public Func<Task<string?>>? ImportPathResolver { get; set; }

    /// <summary>
    /// 파일 저장 UI를 호출하기 위한 콜백입니다.
    /// </summary>
    public Func<Task<string?>>? ExportPathResolver { get; set; }

    /// <summary>
    /// "다른 이름으로 저장" 경로 선택 UI를 호출하기 위한 콜백입니다.
    /// </summary>
    public Func<Task<string?>>? SaveAsPathResolver { get; set; }

    /// <summary>
    /// DOCX를 서식 포함해 저장하는 콜백입니다. (View가 RichTextBox 문서로 저장, 성공 시 true)
    /// </summary>
    public Func<string, Task<bool>>? DocxDocumentSaver { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public MainViewModel(
        DocumentRepository repository,
        BackupService backupService,
        DocumentImportService documentImportService,
        DocxExportService docxExportService,
        TypoCorrectionService typoCorrectionService,
        OllamaService ollamaService,
        ChatService chatService,
        ImageServiceRouter imageService,
        ImageSetupService imageSetupService,
        ComfyUiSetupService comfyUiSetupService,
        HunspellSpellCheckService hunspellService,
        UserDictionaryService userDictionaryService,
        ReferenceLibraryService referenceLibraryService,
        StatisticsService statisticsService,
        LocalizationService localizationService,
        ThemeService themeService,
        SettingsService settingsService,
        NovelProjectService novelProjectService)
    {
        _repository = repository;
        _backupService = backupService;
        _documentImportService = documentImportService;
        _docxExportService = docxExportService;
        _typoCorrectionService = typoCorrectionService;
        _ollamaService = ollamaService;
        _chatService = chatService;
        _imageService = imageService;
        _imageSetupService = imageSetupService;
        _comfyUiSetupService = comfyUiSetupService;
        _selectedHardware = HardwareProfiles[0];
        _hunspellService = hunspellService;
        _userDictionaryService = userDictionaryService;
        _referenceLibraryService = referenceLibraryService;
        _statisticsService = statisticsService;
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;
        _novelProjectService = novelProjectService;

        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync();

        // 편집/스크롤 후 잠시 멈추면 맞춤법을 다시 검사하는 디바운스 타이머입니다.
        _spellCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _spellCheckTimer.Tick += (_, _) =>
        {
            _spellCheckTimer.Stop();
            RunSpellCheck();
        };

        ChatMessages.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoMessages));
    }

    /// <summary>
    /// ViewModel 초기 로딩을 수행합니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        _suppressDirty = true; // 시작 시 로드는 '변경'으로 치지 않음
        await _repository.InitializeAsync();
        var settings = await _settingsService.LoadAsync();
        ApplySettings(settings);

        var (title, content) = await _repository.LoadLatestAsync();
        Title = title;
        Content = content;
        ApplyTheme();
        UpdateStatistics();
        _suppressDirty = false;
        IsProjectDirty = false;

        UpdateAutoSaveTimer();

        LoadReferences();

        // 맞춤법 사전만 시작 시 백그라운드로 로드합니다.
        // AI(Ollama) 확인·모델 목록 로드는 시작 시 하지 않고, 필요할 때(설정 창/AI 기능)에 수행합니다.
        _ = InitializeSpellCheckAsync();
    }

    /// <summary>
    /// AI 준비 상태를 확인합니다. (설정 창 또는 AI 기능 진입 시 호출)
    /// </summary>
    public Task CheckAiReadyAsync() => EnsureAiReadyAsync();

    /// <summary>
    /// 설치된 모델 목록을 불러옵니다. (설정 창 열 때 호출)
    /// </summary>
    public Task RefreshInstalledModelsAsync() => LoadInstalledModelsAsync();

    /// <summary>
    /// 실제로 설치된 모델 이름 목록입니다. (배지 표시용)
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<string> _installedModels = Array.Empty<string>();

    /// <summary>
    /// Ollama에 설치된 모델을 목록에 병합합니다. (추천 목록과 중복 제거)
    /// </summary>
    private async Task LoadInstalledModelsAsync()
    {
        var installed = await _ollamaService.ListInstalledModelsAsync();
        InstalledModels = installed;
        foreach (var name in installed)
        {
            if (!AvailableModels.Contains(name))
            {
                AvailableModels.Add(name);
            }
        }
    }

    /// <summary>
    /// Hunspell 한국어 사전을 로드하고 초기 검사를 수행합니다.
    /// </summary>
    private async Task InitializeSpellCheckAsync()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Dictionaries");
        var loaded = await _hunspellService.LoadAsync(directory);
        if (loaded)
        {
            RunSpellCheck();
        }
        else
        {
            StatusMessage = _localizationService.Get(LanguageCode, "SpellDictMissing");
        }
    }

    [ObservableProperty]
    private string _title = "새 문서";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private int _characterCount;

    [ObservableProperty]
    private int _pageCount = 1;

    [ObservableProperty]
    private int _paragraphCount;

    [ObservableProperty]
    private int _sentenceCount;

    [ObservableProperty]
    private int _dailyWordGoal = 1000;

    [ObservableProperty]
    private int _dailyProgressPercent;

    [ObservableProperty]
    private bool _isFocusMode;

    [ObservableProperty]
    private string _languageCode = "ko-KR";

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _customBackgroundHex = "#FF101010";

    [ObservableProperty]
    private string _customForegroundHex = "#FFF0F0F0";

    [ObservableProperty]
    private int _autoSaveSeconds = 30;

    [ObservableProperty]
    private bool _autoSaveEnabled = true;

    [ObservableProperty]
    private double _menuFontSize = 13;

    [ObservableProperty]
    private double _referenceFontSize = 14;

    [ObservableProperty]
    private string _referenceForegroundHex = "#FFDDDDDD";

    [ObservableProperty]
    private Brush _referenceForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD"));

    [ObservableProperty]
    private string _aiModel = "exaone3.5:7.8b";

    private string _aiBaseUrl = "http://localhost:11434/v1";

    [ObservableProperty]
    private double _toolbarIconSize = 28;

    [ObservableProperty]
    private double _editorFontSize = 30;

    [ObservableProperty]
    private string _editorFontFamilyName = "맑은 고딕";

    [ObservableProperty]
    private string _backgroundImagePath = string.Empty;

    [ObservableProperty]
    private double _backgroundOpacity = 0.3;

    /// <summary>배경 이미지 파일 선택 콜백입니다.</summary>
    public Func<Task<string?>>? BackgroundImageResolver { get; set; }

    /// <summary>선택 가능한 폰트 종류 목록입니다.</summary>
    public IReadOnlyList<string> FontFamilies { get; } = new[]
    {
        "맑은 고딕", "바탕", "굴림", "돋움", "궁서", "나눔고딕", "나눔명조", "함초롬바탕", "Consolas", "Segoe UI", "Times New Roman"
    };

    /// <summary>에디터에 적용할 폰트입니다.</summary>
    public FontFamily EditorFontFamily
    {
        get
        {
            try
            {
                return new FontFamily(string.IsNullOrWhiteSpace(EditorFontFamilyName) ? "맑은 고딕" : EditorFontFamilyName);
            }
            catch
            {
                return new FontFamily("맑은 고딕");
            }
        }
    }

    /// <summary>배경 이미지가 설정되어 있는지 여부입니다.</summary>
    public bool HasBackgroundImage => !string.IsNullOrWhiteSpace(BackgroundImagePath) && File.Exists(BackgroundImagePath);

    /// <summary>배경 이미지 소스입니다.</summary>
    public ImageSource? BackgroundImage
    {
        get
        {
            if (!HasBackgroundImage)
            {
                return null;
            }

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(BackgroundImagePath);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>배경 이미지가 있으면 에디터를 투명(이미지 비침)으로, 없으면 색 배경으로 합니다.</summary>
    public Brush EditorEffectiveBackground => HasBackgroundImage ? Brushes.Transparent : EditorBackground;

    partial void OnEditorFontFamilyNameChanged(string value) => OnPropertyChanged(nameof(EditorFontFamily));

    partial void OnBackgroundImagePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasBackgroundImage));
        OnPropertyChanged(nameof(BackgroundImage));
        OnPropertyChanged(nameof(EditorEffectiveBackground));
    }

    partial void OnEditorBackgroundChanged(Brush value) => OnPropertyChanged(nameof(EditorEffectiveBackground));

    [ObservableProperty]
    private double _chatFontSize = 14;

    [ObservableProperty]
    private string _chatBackgroundHex = "#FF1E1E1E";

    [ObservableProperty]
    private Brush _chatBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E"));

    /// <summary>
    /// 설정에서 고를 수 있는 AI 모델 목록입니다. (추천 + 설치된 모델)
    /// </summary>
    public ObservableCollection<string> AvailableModels { get; } = new(new[]
    {
        // EXAONE (LG, 한국어 강점 · 비상업)
        "exaone3.5:2.4b",
        "exaone3.5:7.8b",
        "exaone3.5:32b",
        "exaone-deep:7.8b",
        "exaone-deep:32b",
        // Qwen 2.5 (한국어 양호 · 상업 허용)
        "qwen2.5:1.5b",
        "qwen2.5:3b",
        "qwen2.5:7b",
        "qwen2.5:14b",
        "qwen2.5:32b",
        // Gemma (Google)
        "gemma2:2b",
        "gemma2:9b",
        "gemma2:27b",
        "gemma3:4b",
        "gemma3:12b",
        "gemma3:27b",
        // Llama (Meta)
        "llama3.2:3b",
        "llama3.1:8b",
        // 기타
        "mistral:7b",
        "phi4",
        "deepseek-r1:7b",
        "deepseek-r1:14b",
        // Magnum v4 (anthracite, 창작/롤플레이 특화 · 커뮤니티 태그)
        "fluffy/magnum-v4-9b",
        "LESSTHANSUPER/MAGNUM_V4-Mistral_Small:12b_Q4_K_S",
        // 무검열(Uncensored/Abliterated) 창작 특화 — uncensored_llm_guide.md 참고
        "dolphin3",
        "mannix/llama3.1-8b-abliterated",
        "nous-hermes3"
    });

    /// <summary>
    /// 색 선택 팔레트입니다.
    /// </summary>
    public IReadOnlyList<string> PaletteColors { get; } = new[]
    {
        "#FFFFFFFF", "#FFDDDDDD", "#FFAAAAAA", "#FF808080", "#FF000000",
        "#FF1E1E1E", "#FF263238", "#FF102027", "#FF1B1B2F", "#FF2C1B1B",
        "#FFEF5350", "#FFEC407A", "#FFAB47BC", "#FF5C6BC0", "#FF29B6F6",
        "#FF26A69A", "#FF66BB6A", "#FFD4E157", "#FFFFEE58", "#FFFFA726"
    };

    [ObservableProperty]
    private bool _isAiSetupVisible;

    [ObservableProperty]
    private string _aiSetupMessage = string.Empty;

    [ObservableProperty]
    private double _aiSetupProgress;

    [ObservableProperty]
    private bool _isAiSetupBusy;

    [ObservableProperty]
    private bool _isAiProgressKnown;

    [ObservableProperty]
    private bool _isOllamaMissing;

    [ObservableProperty]
    private bool _canDownloadModel;

    /// <summary>
    /// 진행률을 알 수 없는 단계(manifest 등)에서 무한 진행 표시 여부입니다.
    /// </summary>
    public bool IsAiProgressIndeterminate => IsAiSetupBusy && !IsAiProgressKnown;

    partial void OnIsAiSetupBusyChanged(bool value) => OnPropertyChanged(nameof(IsAiProgressIndeterminate));

    partial void OnIsAiProgressKnownChanged(bool value) => OnPropertyChanged(nameof(IsAiProgressIndeterminate));

    [ObservableProperty]
    private Brush _editorBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF121212"));

    [ObservableProperty]
    private Brush _editorForeground = new SolidColorBrush(Colors.WhiteSmoke);

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 새 문서를 시작합니다. (현재 내용을 비움)
    /// </summary>
    [RelayCommand]
    private void NewDocument()
    {
        Title = "새 문서";
        Content = string.Empty;
        TypoMarks.Clear();
        StatusMessage = _localizationService.Get(LanguageCode, "NewDocumentDone");
    }

    /// <summary>
    /// 저장 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repository.SaveAsync(Title, Content);
        await _backupService.CreateBackupAsync(Title, Content);
        StatusMessage = _localizationService.Get(LanguageCode, "Saved");
    }

    /// <summary>
    /// 자동 저장을 실행합니다.
    /// </summary>
    private async Task AutoSaveAsync()
    {
        await _repository.SaveAsync(Title, Content);
        StatusMessage = _localizationService.Get(LanguageCode, "AutoSaved");
    }

    /// <summary>
    /// 외부 문서 불러오기 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task OpenAsync()
    {
        if (ImportPathResolver is null)
        {
            return;
        }

        var path = await ImportPathResolver();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            var (title, content) = await _documentImportService.ImportAsync(path);
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(path) : title;
            Content = content;
            await _repository.SaveAsync(Title, Content);
            StatusMessage = _localizationService.Get(LanguageCode, "Opened");
        }
        catch
        {
            StatusMessage = _localizationService.Get(LanguageCode, "OpenFailed");
        }
    }

    /// <summary>
    /// DOCX 내보내기 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (ExportPathResolver is null)
        {
            return;
        }

        var path = await ExportPathResolver();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (DocxDocumentSaver is null || !await DocxDocumentSaver(path))
        {
            await _docxExportService.ExportAsync(path, Title, Content);
        }

        StatusMessage = _localizationService.Get(LanguageCode, "Saved");
    }

    /// <summary>
    /// 다른 이름으로 저장합니다. (.txt/.md는 텍스트, .docx는 Word로 저장)
    /// </summary>
    [RelayCommand]
    private async Task SaveAsAsync()
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

        if (Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase))
        {
            // 서식 포함 저장(콜백)이 있으면 우선 사용, 없으면 평문 DOCX로 대체합니다.
            if (DocxDocumentSaver is null || !await DocxDocumentSaver(path))
            {
                await _docxExportService.ExportAsync(path, Title, Content);
            }
        }
        else
        {
            await File.WriteAllTextAsync(path, Content ?? string.Empty);
        }

        StatusMessage = _localizationService.Get(LanguageCode, "Saved");
    }

    /// <summary>
    /// 현재 보이는 페이지의 맞춤법을 즉시 다시 검사합니다. (버튼)
    /// </summary>
    [RelayCommand]
    private void FixTypos()
    {
        _spellCheckTimer.Stop();
        RunSpellCheck();
    }

    /// <summary>
    /// 편집/스크롤 후 디바운스로 재검사를 예약합니다. (뷰에서 호출)
    /// </summary>
    public void RequestSpellCheck()
    {
        if (!_hunspellService.IsReady)
        {
            return;
        }

        _spellCheckTimer.Stop();
        _spellCheckTimer.Start();
    }

    /// <summary>
    /// AI로 현재 보이는 페이지의 문맥/문법 오류를 검사하여 파란 밑줄로 표시합니다. (③ 단계)
    /// </summary>
    [RelayCommand]
    private async Task CheckContextAsync()
    {
        var (offset, target) = GetVisibleTarget();
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        StatusMessage = _localizationService.Get(LanguageCode, "AiContextChecking");
        var pairs = await _typoCorrectionService.DetectAsync(target);

        RemoveMarksOfKind(TypoKind.Context);

        _suppressMarkClear = true;
        var count = 0;
        foreach (var pair in pairs)
        {
            var from = 0;
            while (from <= target.Length)
            {
                var index = target.IndexOf(pair.Wrong, from, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                TypoMarks.Add(new TypoMark
                {
                    Kind = TypoKind.Context,
                    Start = offset + index,
                    Length = pair.Wrong.Length,
                    Wrong = pair.Wrong,
                    Right = pair.Right,
                    Suggestions = new[] { pair.Right }
                });
                from = index + pair.Wrong.Length;
                count++;
            }
        }

        _suppressMarkClear = false;

        StatusMessage = count > 0
            ? string.Format(_localizationService.Get(LanguageCode, "ContextFound"), count)
            : _localizationService.Get(LanguageCode, "NoContextIssues");
    }

    /// <summary>
    /// 현재 보이는 범위의 어절을 Hunspell로 검사하여 맞춤법 오타를 표시합니다.
    /// </summary>
    private void RunSpellCheck()
    {
        if (!_hunspellService.IsReady)
        {
            return;
        }

        var (offset, target) = GetVisibleTarget();

        var marks = new List<TypoMark>();
        foreach (Match match in HangulWordRegex.Matches(target))
        {
            var word = match.Value;

            // ① 사용자 사전 / 세션 무시 목록을 먼저 확인합니다.
            if (_userDictionaryService.Contains(word) || _ignoredWords.Contains(word))
            {
                continue;
            }

            // ② Hunspell 사전 검사 (정상이면 건너뜀)
            if (_hunspellService.Check(word))
            {
                continue;
            }

            marks.Add(new TypoMark
            {
                Kind = TypoKind.Spelling,
                Start = offset + match.Index,
                Length = word.Length,
                Wrong = word
            });
        }

        // 맞춤법(빨강) 마크만 교체하고 문맥(파랑) 마크는 유지합니다.
        _suppressMarkClear = true;
        RemoveMarksOfKind(TypoKind.Spelling);
        foreach (var mark in marks)
        {
            TypoMarks.Add(mark);
        }

        _suppressMarkClear = false;

        StatusMessage = marks.Count > 0
            ? string.Format(_localizationService.Get(LanguageCode, "TyposFound"), marks.Count)
            : _localizationService.Get(LanguageCode, "NoTypos");
    }

    private (int Offset, string Target) GetVisibleTarget()
    {
        var offset = 0;
        var target = Content;

        var range = VisibleRangeResolver?.Invoke();
        if (range is { } r && r.Length > 0 && r.Start >= 0 && r.Start < Content.Length)
        {
            offset = r.Start;
            var length = Math.Min(r.Length, Content.Length - r.Start);
            target = Content.Substring(r.Start, length);
        }

        return (offset, target);
    }

    private void RemoveMarksOfKind(TypoKind kind)
    {
        for (var i = TypoMarks.Count - 1; i >= 0; i--)
        {
            if (TypoMarks[i].Kind == kind)
            {
                TypoMarks.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 오타의 교정 추천을 반환합니다. (필요 시점에 계산하여 캐시)
    /// </summary>
    public IReadOnlyList<string> GetSuggestions(TypoMark mark)
    {
        if (mark.Suggestions.Count > 0)
        {
            return mark.Suggestions;
        }

        mark.Suggestions = mark.Kind == TypoKind.Spelling
            ? _hunspellService.Suggest(mark.Wrong)
            : string.IsNullOrEmpty(mark.Right) ? Array.Empty<string>() : new[] { mark.Right };

        return mark.Suggestions;
    }

    /// <summary>
    /// 오타를 지정한 표현으로 교체합니다.
    /// </summary>
    public bool ApplyReplacement(TypoMark mark, string replacement)
    {
        if (mark.End > Content.Length)
        {
            return false;
        }

        var delta = replacement.Length - mark.Length;

        _suppressMarkClear = true;
        Content = string.Concat(Content.AsSpan(0, mark.Start), replacement, Content.AsSpan(mark.End));
        _suppressMarkClear = false;

        TypoMarks.Remove(mark);
        foreach (var other in TypoMarks)
        {
            if (other.Start >= mark.End)
            {
                other.Start += delta;
            }
        }

        StatusMessage = _localizationService.Get(LanguageCode, "AiFixed");
        return true;
    }

    /// <summary>
    /// 해당 단어를 이번 세션 동안 오타로 표시하지 않습니다.
    /// </summary>
    public void IgnoreWord(TypoMark mark)
    {
        _ignoredWords.Add(mark.Wrong);
        RemoveMarksOfWord(mark.Wrong);
    }

    /// <summary>
    /// 해당 단어를 사용자 사전에 추가합니다. (이후 항상 정상으로 처리)
    /// </summary>
    public void AddWordToDictionary(TypoMark mark)
    {
        _userDictionaryService.Add(mark.Wrong);
        RemoveMarksOfWord(mark.Wrong);
    }

    private void RemoveMarksOfWord(string word)
    {
        for (var i = TypoMarks.Count - 1; i >= 0; i--)
        {
            if (TypoMarks[i].Wrong == word)
            {
                TypoMarks.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 집중 모드 전환 명령입니다.
    /// </summary>
    [RelayCommand]
    private void ToggleFocusMode()
    {
        IsFocusMode = !IsFocusMode;
    }

    /// <summary>
    /// 다크 테마 적용 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SetDarkThemeAsync()
    {
        Theme = "Dark";
        ApplyTheme();
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 라이트 테마 적용 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SetLightThemeAsync()
    {
        Theme = "Light";
        ApplyTheme();
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 커스텀 테마 적용 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SetCustomThemeAsync()
    {
        Theme = "Custom";
        ApplyTheme();
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 한국어로 UI 언어를 설정합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetKoreanAsync()
    {
        LanguageCode = "ko-KR";
        RaiseLocalizedPropertiesChanged();
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 영어로 UI 언어를 설정합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetEnglishAsync()
    {
        LanguageCode = "en-US";
        RaiseLocalizedPropertiesChanged();
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 참고자료 글자 색을 팔레트에서 선택한 색으로 설정합니다.
    /// </summary>
    [RelayCommand]
    private void SetReferenceColor(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            ReferenceForegroundHex = hex;
        }
    }

    /// <summary>
    /// 채팅 배경색을 팔레트에서 선택한 색으로 설정합니다.
    /// </summary>
    [RelayCommand]
    private void SetChatBackground(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            ChatBackgroundHex = hex;
        }
    }

    /// <summary>
    /// 커스텀 테마 배경색을 팔레트에서 선택한 색으로 설정하고 커스텀 테마를 적용합니다.
    /// </summary>
    [RelayCommand]
    private void SetCustomBackground(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        CustomBackgroundHex = hex;
        Theme = "Custom";
        ApplyTheme();
    }

    /// <summary>
    /// 커스텀 테마 글자색을 팔레트에서 선택한 색으로 설정하고 커스텀 테마를 적용합니다.
    /// </summary>
    [RelayCommand]
    private void SetCustomForeground(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }

        CustomForegroundHex = hex;
        Theme = "Custom";
        ApplyTheme();
    }

    /// <summary>
    /// 배경 이미지 파일을 선택합니다.
    /// </summary>
    [RelayCommand]
    private async Task OpenBackgroundImageAsync()
    {
        if (BackgroundImageResolver is null)
        {
            return;
        }

        var path = await BackgroundImageResolver();
        if (!string.IsNullOrWhiteSpace(path))
        {
            BackgroundImagePath = path;
        }
    }

    /// <summary>
    /// 배경 이미지를 제거합니다.
    /// </summary>
    [RelayCommand]
    private void ClearBackgroundImage() => BackgroundImagePath = string.Empty;

    /// <summary>
    /// Ollama 실행 여부와 선택 모델 설치 여부를 확인하고, 필요한 안내를 표시합니다.
    /// </summary>
    private async Task EnsureAiReadyAsync()
    {
        IsAiSetupBusy = false;
        IsAiProgressKnown = false;
        AiSetupProgress = 0;
        CanDownloadModel = false;
        IsAiSetupVisible = true;
        AiSetupMessage = _localizationService.Get(LanguageCode, "AiSetupChecking");

        if (!await _ollamaService.IsRunningAsync())
        {
            IsOllamaMissing = true;
            AiSetupMessage = _localizationService.Get(LanguageCode, "AiSetupOllamaMissing");
            return;
        }

        IsOllamaMissing = false;

        if (await _ollamaService.IsModelInstalledAsync(AiModel))
        {
            IsAiSetupVisible = false;
            return;
        }

        // 모델이 없으면 사용자 동의(다운로드 버튼) 후 받습니다. (용량이 크므로 자동 시작하지 않음)
        CanDownloadModel = true;
        AiSetupMessage = string.Format(_localizationService.Get(LanguageCode, "AiSetupModelMissing"), AiModel);
    }

    /// <summary>
    /// 선택한 모델을 다운로드합니다.
    /// </summary>
    [RelayCommand]
    private async Task DownloadModelAsync()
    {
        CanDownloadModel = false;
        IsAiSetupBusy = true;
        IsAiProgressKnown = false;
        AiSetupProgress = 0;
        AiSetupMessage = _localizationService.Get(LanguageCode, "AiSetupPreparing");

        _pullCancellation?.Cancel();
        _pullCancellation = new CancellationTokenSource();

        var progress = new Progress<OllamaPullProgress>(p =>
        {
            if (p.Percent >= 0)
            {
                IsAiProgressKnown = true;
                AiSetupProgress = p.Percent;
                AiSetupMessage = string.Format(_localizationService.Get(LanguageCode, "AiSetupDownloading"), Math.Round(p.Percent));
            }
            else
            {
                AiSetupMessage = _localizationService.Get(LanguageCode, "AiSetupPreparing");
            }
        });

        try
        {
            await _ollamaService.PullModelAsync(AiModel, progress, _pullCancellation.Token);
            IsAiSetupBusy = false;
            IsAiSetupVisible = false;
            StatusMessage = _localizationService.Get(LanguageCode, "AiReady");
        }
        catch (OperationCanceledException)
        {
            IsAiSetupBusy = false;
            CanDownloadModel = true;
            AiSetupMessage = string.Format(_localizationService.Get(LanguageCode, "AiSetupModelMissing"), AiModel);
        }
        catch
        {
            IsAiSetupBusy = false;
            CanDownloadModel = true;
            AiSetupMessage = _localizationService.Get(LanguageCode, "AiSetupDownloadFailed");
        }
    }

    /// <summary>
    /// 진행 중인 모델 다운로드를 취소합니다.
    /// </summary>
    [RelayCommand]
    private void CancelDownload()
    {
        _pullCancellation?.Cancel();
    }

    /// <summary>
    /// AI 준비 상태를 다시 확인합니다.
    /// </summary>
    [RelayCommand]
    private async Task RetryAiSetupAsync()
    {
        await EnsureAiReadyAsync();
    }

    /// <summary>
    /// Ollama 공식 다운로드 페이지를 브라우저로 엽니다.
    /// </summary>
    [RelayCommand]
    private void OpenOllamaSite()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ollama.com/download",
                UseShellExecute = true
            });
        }
        catch
        {
            // 브라우저 실행 실패는 무시합니다.
        }
    }

    /// <summary>
    /// AI 준비 안내를 닫고 나중으로 미룹니다.
    /// </summary>
    [RelayCommand]
    private void DismissAiSetup()
    {
        IsAiSetupVisible = false;
    }

    /// <summary>
    /// 현재 설정을 파일에 저장합니다. (설정 창에서 호출)
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await PersistSettingsAsync();
        // 모델이 바뀌었을 수 있으므로 준비 상태를 다시 확인합니다.
        await EnsureAiReadyAsync();
    }

    /// <summary>
    /// 참고자료 서랍을 열거나 닫습니다.
    /// </summary>
    [RelayCommand]
    private void ToggleReferenceDrawer()
    {
        IsReferenceDrawerOpen = !IsReferenceDrawerOpen;
    }

    /// <summary>
    /// 참고자료 유형별 하위 폴더 이름입니다. (생성기의 SubFolderFor와 일치)
    /// </summary>
    private static readonly string[] ReferenceSubFolders =
    {
        "Characters", "World", "Backgrounds", "Synopsis", "Descriptions", "Illustrations"
    };

    /// <summary>
    /// 참고자료 루트 폴더를 지정(생성)하고, 유형별 하위 폴더를 함께 만듭니다. (소설별로 분리 관리)
    /// </summary>
    [RelayCommand]
    private async Task CreateReferenceFolderAsync()
    {
        if (ReferenceFolderResolver is null)
        {
            return;
        }

        var location = await ReferenceFolderResolver();
        if (string.IsNullOrWhiteSpace(location))
        {
            return;
        }

        // 선택한 위치 안에 '소설 제목' 폴더를 자동 생성하고, 그 안에 유형별 하위 폴더까지 만듭니다.
        var novelName = MakeSafeFolderName(string.IsNullOrWhiteSpace(Title) || Title == "새 문서" ? "새 소설" : Title);
        var root = Path.Combine(location, novelName);

        try
        {
            Directory.CreateDirectory(root);
            foreach (var sub in ReferenceSubFolders)
            {
                Directory.CreateDirectory(Path.Combine(root, sub));
            }
        }
        catch (IOException)
        {
            // 폴더 생성 실패는 무시합니다.
        }

        _referenceFolder = root;
        LoadReferences();
        IsReferenceDrawerOpen = true;
        StatusMessage = string.Format(_localizationService.Get(LanguageCode, "ReferenceFolderCreated"), novelName);
        await PersistSettingsAsync();
    }

    private static string MakeSafeFolderName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }

    /// <summary>
    /// 참고자료 폴더를 선택하고 목록을 불러옵니다.
    /// </summary>
    [RelayCommand]
    private async Task OpenReferenceFolderAsync()
    {
        if (ReferenceFolderResolver is null)
        {
            return;
        }

        var folder = await ReferenceFolderResolver();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        _referenceFolder = folder;
        LoadReferences();
        IsReferenceDrawerOpen = true;
        await PersistSettingsAsync();
    }

    /// <summary>
    /// 현재 참고자료 폴더를 다시 스캔합니다.
    /// </summary>
    [RelayCommand]
    private void RefreshReferences()
    {
        LoadReferences();
    }

    private void LoadReferences()
    {
        var previouslySelected = SelectedReference?.FullPath;

        References.Clear();
        foreach (var document in _referenceLibraryService.LoadFolder(_referenceFolder))
        {
            References.Add(document);
        }

        SelectedReference = References.FirstOrDefault(d => d.FullPath == previouslySelected)
            ?? References.FirstOrDefault();

        OnPropertyChanged(nameof(HasNoReferences));
    }

    /// <summary>
    /// AI 채팅 서랍을 열거나 닫습니다.
    /// </summary>
    [RelayCommand]
    private void ToggleChatDrawer()
    {
        IsChatDrawerOpen = !IsChatDrawerOpen;
    }

    /// <summary>
    /// 채팅 대화 기록을 지웁니다.
    /// </summary>
    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
    }

    /// <summary>
    /// 현재 입력을 AI에게 보내고 응답을 받습니다.
    /// </summary>
    [RelayCommand]
    private async Task SendChatAsync()
    {
        var question = ChatInput.Trim();
        if (string.IsNullOrEmpty(question) || IsChatBusy)
        {
            return;
        }

        ChatInput = string.Empty;
        ChatMessages.Add(new ChatMessage { IsUser = true, Text = question });
        IsChatBusy = true;

        // 시스템 지시 + 대화 히스토리를 구성합니다.
        var turns = new List<ChatTurn>
        {
            new("system",
                "You are a helpful writing assistant for a novelist. "
                + "Answer concisely and helpfully in the user's language (Korean or English). "
                + "Help with plot ideas, character development, phrasing, and questions about writing.")
        };

        foreach (var message in ChatMessages)
        {
            turns.Add(new ChatTurn(message.IsUser ? "user" : "assistant", message.Text));
        }

        var answer = await _chatService.AskAsync(turns);

        ChatMessages.Add(new ChatMessage
        {
            IsUser = false,
            Text = string.IsNullOrWhiteSpace(answer)
                ? _localizationService.Get(LanguageCode, "ChatFailed")
                : answer
        });

        IsChatBusy = false;
    }

    partial void OnContentChanged(string value)
    {
        UpdateStatistics();
        MarkProjectDirty();

        // 사용자가 직접 편집하면 잠시 후 맞춤법을 다시 검사합니다.
        // (오타 검사/교체가 유발한 변경은 억제합니다)
        if (!_suppressMarkClear)
        {
            RequestSpellCheck();
        }
    }

    partial void OnDailyWordGoalChanged(int value)
    {
        UpdateProgress();
    }

    partial void OnAiModelChanged(string value)
    {
        _typoCorrectionService.Model = value;
        _chatService.Model = value;
    }

    partial void OnChatBackgroundHexChanged(string value)
    {
        try
        {
            ChatBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            // 잘못된 색 문자열은 무시합니다.
        }
    }

    partial void OnAutoSaveEnabledChanged(bool value) => UpdateAutoSaveTimer();

    partial void OnAutoSaveSecondsChanged(int value) => UpdateAutoSaveTimer();

    partial void OnReferenceForegroundHexChanged(string value)
    {
        try
        {
            ReferenceForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        }
        catch
        {
            // 잘못된 색 문자열은 무시합니다.
        }
    }

    private void UpdateAutoSaveTimer()
    {
        _autoSaveTimer.Stop();
        if (AutoSaveEnabled)
        {
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(Math.Max(10, AutoSaveSeconds));
            _autoSaveTimer.Start();
        }
    }

    partial void OnCustomBackgroundHexChanged(string value)
    {
        if (Theme == "Custom")
        {
            ApplyTheme();
        }
    }

    partial void OnCustomForegroundHexChanged(string value)
    {
        if (Theme == "Custom")
        {
            ApplyTheme();
        }
    }

    /// <summary>
    /// 다국어 타이틀 플레이스홀더를 반환합니다.
    /// </summary>
    public string TitlePlaceholder => _localizationService.Get(LanguageCode, "TitlePlaceholder");

    /// <summary>
    /// 파일 열기 메뉴 텍스트를 반환합니다.
    /// </summary>
    public string OpenText => _localizationService.Get(LanguageCode, "OpenButton");

    /// <summary>
    /// 저장 버튼 텍스트를 반환합니다.
    /// </summary>
    public string SaveText => _localizationService.Get(LanguageCode, "SaveButton");

    /// <summary>
    /// DOCX 내보내기 버튼 텍스트를 반환합니다.
    /// </summary>
    public string ExportText => _localizationService.Get(LanguageCode, "ExportButton");

    /// <summary>
    /// AI 보정 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiFixText => _localizationService.Get(LanguageCode, "AiFixButton");

    /// <summary>
    /// 집중 모드 버튼 텍스트를 반환합니다.
    /// </summary>
    public string FocusText => _localizationService.Get(LanguageCode, "FocusButton");

    /// <summary>
    /// 통계 헤더 텍스트를 반환합니다.
    /// </summary>
    public string StatsText => _localizationService.Get(LanguageCode, "Stats");

    /// <summary>
    /// 파일 메뉴 텍스트를 반환합니다.
    /// </summary>
    public string FileMenuText => _localizationService.Get(LanguageCode, "FileMenu");

    /// <summary>
    /// 보기 메뉴 텍스트를 반환합니다.
    /// </summary>
    public string ViewMenuText => _localizationService.Get(LanguageCode, "ViewMenu");

    /// <summary>
    /// 테마 메뉴 텍스트를 반환합니다.
    /// </summary>
    public string ThemeMenuText => _localizationService.Get(LanguageCode, "ThemeMenu");

    /// <summary>
    /// 다크 테마 텍스트를 반환합니다.
    /// </summary>
    public string ThemeDarkText => _localizationService.Get(LanguageCode, "ThemeDark");

    /// <summary>
    /// 라이트 테마 텍스트를 반환합니다.
    /// </summary>
    public string ThemeLightText => _localizationService.Get(LanguageCode, "ThemeLight");

    /// <summary>
    /// 커스텀 테마 텍스트를 반환합니다.
    /// </summary>
    public string ThemeCustomText => _localizationService.Get(LanguageCode, "ThemeCustom");

    /// <summary>
    /// 언어 메뉴 텍스트를 반환합니다.
    /// </summary>
    public string LanguageText => _localizationService.Get(LanguageCode, "Language");

    /// <summary>
    /// 한국어 언어 항목 텍스트를 반환합니다.
    /// </summary>
    public string KoreanText => _localizationService.Get(LanguageCode, "LanguageKorean");

    /// <summary>
    /// 영어 언어 항목 텍스트를 반환합니다.
    /// </summary>
    public string EnglishText => _localizationService.Get(LanguageCode, "LanguageEnglish");

    /// <summary>
    /// AI 준비 안내 제목 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupTitleText => _localizationService.Get(LanguageCode, "AiSetupTitle");

    /// <summary>
    /// 모델 다운로드 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupDownloadText => _localizationService.Get(LanguageCode, "AiSetupDownloadButton");

    /// <summary>
    /// Ollama 설치 페이지 열기 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupOpenOllamaText => _localizationService.Get(LanguageCode, "AiSetupOpenOllama");

    /// <summary>
    /// 다시 확인 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupRetryText => _localizationService.Get(LanguageCode, "AiSetupRetry");

    /// <summary>
    /// 나중에(닫기) 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupDismissText => _localizationService.Get(LanguageCode, "AiSetupDismiss");

    /// <summary>
    /// 다운로드 취소 버튼 텍스트를 반환합니다.
    /// </summary>
    public string AiSetupCancelText => _localizationService.Get(LanguageCode, "AiSetupCancel");

    /// <summary>
    /// 참고자료 서랍 제목 텍스트를 반환합니다.
    /// </summary>
    public string ReferenceTitleText => _localizationService.Get(LanguageCode, "ReferenceTitle");

    /// <summary>
    /// 참고자료 버튼/폴더 열기 텍스트를 반환합니다.
    /// </summary>
    public string ReferenceOpenFolderText => _localizationService.Get(LanguageCode, "ReferenceOpenFolder");

    /// <summary>
    /// 참고자료 새로고침 텍스트를 반환합니다.
    /// </summary>
    public string ReferenceRefreshText => _localizationService.Get(LanguageCode, "ReferenceRefresh");

    /// <summary>
    /// 참고자료 빈 상태 안내 텍스트를 반환합니다.
    /// </summary>
    public string ReferenceEmptyText => _localizationService.Get(LanguageCode, "ReferenceEmpty");

    /// <summary>
    /// AI 채팅 서랍 제목 텍스트를 반환합니다.
    /// </summary>
    public string ChatTitleText => _localizationService.Get(LanguageCode, "ChatTitle");

    /// <summary>
    /// 채팅 입력창 안내 텍스트를 반환합니다.
    /// </summary>
    public string ChatInputHintText => _localizationService.Get(LanguageCode, "ChatInputHint");

    /// <summary>
    /// 채팅 빈 상태 안내 텍스트를 반환합니다.
    /// </summary>
    public string ChatEmptyText => _localizationService.Get(LanguageCode, "ChatEmpty");

    /// <summary>
    /// 오타 우클릭 메뉴 "무시" 텍스트를 반환합니다.
    /// </summary>
    public string IgnoreWordText => _localizationService.Get(LanguageCode, "IgnoreWord");

    /// <summary>
    /// 오타 우클릭 메뉴 "사전에 추가" 텍스트를 반환합니다.
    /// </summary>
    public string AddToDictionaryText => _localizationService.Get(LanguageCode, "AddToUserDictionary");

    /// <summary>
    /// 추천이 없을 때 표시할 텍스트를 반환합니다.
    /// </summary>
    public string NoSuggestionsText => _localizationService.Get(LanguageCode, "NoSuggestions");

    /// <summary>AI 문맥 검사 버튼 텍스트입니다.</summary>
    public string ContextCheckText => _localizationService.Get(LanguageCode, "ContextCheck");

    /// <summary>다른 이름으로 저장 텍스트입니다.</summary>
    public string SaveAsText => _localizationService.Get(LanguageCode, "SaveAsButton");

    /// <summary>새 문서 텍스트입니다.</summary>
    public string NewDocumentText => _localizationService.Get(LanguageCode, "NewDocument");

    /// <summary>설정 메뉴 텍스트입니다.</summary>
    public string SettingsMenuText => _localizationService.Get(LanguageCode, "SettingsMenu");

    /// <summary>설정 창 제목 텍스트입니다.</summary>
    public string SettingsTitleText => _localizationService.Get(LanguageCode, "SettingsTitle");

    /// <summary>자동 저장 사용 라벨 텍스트입니다.</summary>
    public string AutoSaveLabelText => _localizationService.Get(LanguageCode, "AutoSaveLabel");

    /// <summary>자동 저장 주기 라벨 텍스트입니다.</summary>
    public string AutoSaveIntervalLabelText => _localizationService.Get(LanguageCode, "AutoSaveIntervalLabel");

    /// <summary>메뉴 폰트 크기 라벨 텍스트입니다.</summary>
    public string MenuFontSizeLabelText => _localizationService.Get(LanguageCode, "MenuFontSizeLabel");

    /// <summary>참고자료 폰트 크기 라벨 텍스트입니다.</summary>
    public string ReferenceFontSizeLabelText => _localizationService.Get(LanguageCode, "ReferenceFontSizeLabel");

    /// <summary>참고자료 색 라벨 텍스트입니다.</summary>
    public string ReferenceColorLabelText => _localizationService.Get(LanguageCode, "ReferenceColorLabel");

    /// <summary>설정 저장 버튼 텍스트입니다.</summary>
    public string SettingsSaveText => _localizationService.Get(LanguageCode, "SettingsSave");

    /// <summary>설정 닫기 버튼 텍스트입니다.</summary>
    public string SettingsCloseText => _localizationService.Get(LanguageCode, "SettingsClose");

    /// <summary>AI 모델 라벨 텍스트입니다.</summary>
    public string AiModelLabelText => _localizationService.Get(LanguageCode, "AiModelLabel");

    /// <summary>툴바 아이콘 크기 라벨 텍스트입니다.</summary>
    public string ToolbarIconSizeLabelText => _localizationService.Get(LanguageCode, "ToolbarIconSizeLabel");

    /// <summary>AI 어시스턴트 폰트 크기 라벨 텍스트입니다.</summary>
    public string ChatFontSizeLabelText => _localizationService.Get(LanguageCode, "ChatFontSizeLabel");

    /// <summary>AI 어시스턴트 배경색 라벨 텍스트입니다.</summary>
    public string ChatBackgroundLabelText => _localizationService.Get(LanguageCode, "ChatBackgroundLabel");

    /// <summary>편집기 폰트 크기 라벨 텍스트입니다.</summary>
    public string EditorFontSizeLabelText => _localizationService.Get(LanguageCode, "EditorFontSizeLabel");

    /// <summary>커스텀 테마 라벨 텍스트입니다.</summary>
    public string CustomThemeLabelText => _localizationService.Get(LanguageCode, "CustomThemeLabel");

    /// <summary>커스텀 배경색 라벨 텍스트입니다.</summary>
    public string CustomBackgroundLabelText => _localizationService.Get(LanguageCode, "CustomBackgroundLabel");

    /// <summary>커스텀 글자색 라벨 텍스트입니다.</summary>
    public string CustomForegroundLabelText => _localizationService.Get(LanguageCode, "CustomForegroundLabel");

    /// <summary>
    /// 목표 라벨 텍스트를 반환합니다.
    /// </summary>
    public string GoalLabelText => _localizationService.Get(LanguageCode, "GoalLabel");

    /// <summary>
    /// 진행률 텍스트를 반환합니다.
    /// </summary>
    public string ProgressText => _localizationService.Get(LanguageCode, "Progress");

    /// <summary>
    /// 단어 라벨 텍스트를 반환합니다.
    /// </summary>
    public string WordText => _localizationService.Get(LanguageCode, "Word");

    /// <summary>
    /// 문자 라벨 텍스트를 반환합니다.
    /// </summary>
    public string CharacterText => _localizationService.Get(LanguageCode, "Character");

    /// <summary>
    /// 페이지 라벨 텍스트를 반환합니다.
    /// </summary>
    public string PageText => _localizationService.Get(LanguageCode, "Page");

    /// <summary>
    /// 단락 라벨 텍스트를 반환합니다.
    /// </summary>
    public string ParagraphText => _localizationService.Get(LanguageCode, "Paragraph");

    /// <summary>
    /// 문장 라벨 텍스트를 반환합니다.
    /// </summary>
    public string SentenceText => _localizationService.Get(LanguageCode, "Sentence");

    private void UpdateStatistics()
    {
        var stats = _statisticsService.Calculate(Content);
        WordCount = stats.WordCount;
        CharacterCount = stats.CharacterCount;
        PageCount = stats.PageCount;
        ParagraphCount = stats.ParagraphCount;
        SentenceCount = stats.SentenceCount;
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        if (DailyWordGoal <= 0)
        {
            DailyProgressPercent = 0;
            return;
        }

        DailyProgressPercent = Math.Min(100, (int)Math.Round(WordCount / (double)DailyWordGoal * 100));
    }

    /// <summary>
    /// OpenAI 호환 주소(.../v1)를 Ollama 네이티브 API 주소로 변환합니다.
    /// </summary>
    private static string ToOllamaNativeUrl(string baseUrl)
    {
        var trimmed = (baseUrl ?? string.Empty).TrimEnd('/');
        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^3];
        }

        return string.IsNullOrWhiteSpace(trimmed) ? "http://localhost:11434" : trimmed;
    }

    private void ApplyTheme()
    {
        var (background, foreground) = _themeService.Resolve(Theme, CustomBackgroundHex, CustomForegroundHex);
        EditorBackground = background;
        EditorForeground = foreground;
    }

    private async Task PersistSettingsAsync()
    {
        await _settingsService.SaveAsync(new AppSettings
        {
            Theme = Theme,
            LanguageCode = LanguageCode,
            CustomBackgroundHex = CustomBackgroundHex,
            CustomForegroundHex = CustomForegroundHex,
            AutoSaveSeconds = AutoSaveSeconds,
            DailyWordGoal = DailyWordGoal,
            AiModel = AiModel,
            AiBaseUrl = _aiBaseUrl,
            ReferenceFolder = _referenceFolder,
            AutoSaveEnabled = AutoSaveEnabled,
            MenuFontSize = MenuFontSize,
            ReferenceFontSize = ReferenceFontSize,
            ReferenceForegroundHex = ReferenceForegroundHex,
            ToolbarIconSize = ToolbarIconSize,
            ChatFontSize = ChatFontSize,
            ChatBackgroundHex = ChatBackgroundHex,
            EditorFontSize = EditorFontSize,
            EditorFontFamily = EditorFontFamilyName,
            BackgroundImagePath = BackgroundImagePath,
            BackgroundOpacity = BackgroundOpacity,
            ImageBaseUrl = ImageBaseUrl,
            ImageWebUiPath = ImageWebUiPath,
            ImageBackend = UseComfyUi ? "ComfyUI" : "A1111",
            ComfyUiBaseUrl = ComfyUiBaseUrl,
            ComfyUiPath = ComfyUiPath,
            ComfyUiCheckpoint = _imageService.Comfy.CheckpointName,
            ImageHardware = (SelectedHardware ?? HardwareProfiles[0]).Key,
            ImageStyle = CurrentStyle()
        });
    }

    private void ApplySettings(AppSettings settings)
    {
        Theme = settings.Theme;
        LanguageCode = settings.LanguageCode;
        CustomBackgroundHex = settings.CustomBackgroundHex;
        CustomForegroundHex = settings.CustomForegroundHex;
        AutoSaveSeconds = settings.AutoSaveSeconds;
        AutoSaveEnabled = settings.AutoSaveEnabled;
        MenuFontSize = settings.MenuFontSize <= 0 ? 13 : settings.MenuFontSize;
        ReferenceFontSize = settings.ReferenceFontSize <= 0 ? 14 : settings.ReferenceFontSize;
        ReferenceForegroundHex = string.IsNullOrWhiteSpace(settings.ReferenceForegroundHex)
            ? "#FFDDDDDD"
            : settings.ReferenceForegroundHex;
        ToolbarIconSize = settings.ToolbarIconSize <= 0 ? 28 : settings.ToolbarIconSize;
        ChatFontSize = settings.ChatFontSize <= 0 ? 14 : settings.ChatFontSize;
        ChatBackgroundHex = string.IsNullOrWhiteSpace(settings.ChatBackgroundHex)
            ? "#FF1E1E1E"
            : settings.ChatBackgroundHex;
        EditorFontSize = settings.EditorFontSize <= 0 ? 30 : settings.EditorFontSize;
        EditorFontFamilyName = string.IsNullOrWhiteSpace(settings.EditorFontFamily) ? "맑은 고딕" : settings.EditorFontFamily;
        BackgroundImagePath = settings.BackgroundImagePath ?? string.Empty;
        BackgroundOpacity = settings.BackgroundOpacity <= 0 ? 0.3 : settings.BackgroundOpacity;
        DailyWordGoal = settings.DailyWordGoal;
        AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "exaone3.5:7.8b" : settings.AiModel;
        _aiBaseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "http://localhost:11434/v1" : settings.AiBaseUrl;
        _typoCorrectionService.Model = AiModel;
        _typoCorrectionService.BaseUrl = _aiBaseUrl;
        _chatService.Model = AiModel;
        _chatService.BaseUrl = _aiBaseUrl;
        _ollamaService.BaseUrl = ToOllamaNativeUrl(_aiBaseUrl);
        ImageBaseUrl = string.IsNullOrWhiteSpace(settings.ImageBaseUrl) ? "http://127.0.0.1:7860" : settings.ImageBaseUrl;
        ImageWebUiPath = settings.ImageWebUiPath ?? string.Empty;
        ComfyUiBaseUrl = string.IsNullOrWhiteSpace(settings.ComfyUiBaseUrl) ? "http://127.0.0.1:8188" : settings.ComfyUiBaseUrl;
        ComfyUiPath = settings.ComfyUiPath ?? string.Empty;
        UseComfyUi = true; // A1111 제거 — ComfyUI만 사용
        SelectedHardware = HardwareProfiles.FirstOrDefault(h => string.Equals(h.Key, settings.ImageHardware, StringComparison.OrdinalIgnoreCase)) ?? HardwareProfiles[0];
        _imageService.A1111.BaseUrl = ImageBaseUrl;
        _imageService.Comfy.BaseUrl = ComfyUiBaseUrl;
        _imageService.Backend = UseComfyUi ? ImageBackendKind.ComfyUi : ImageBackendKind.A1111;
        LoadStyle(settings.ImageStyle);
        if (!string.IsNullOrWhiteSpace(settings.ComfyUiCheckpoint))
        {
            _imageService.Comfy.CheckpointName = settings.ComfyUiCheckpoint;
            // 저장된 체크포인트가 추천 모델이면 권장 샘플링 설정도 복원합니다.
            var known = ComfyUiSetupService.RecommendedModels
                .FirstOrDefault(m => string.Equals(m.FileName, settings.ComfyUiCheckpoint, StringComparison.OrdinalIgnoreCase));
            if (known is not null)
            {
                ApplyComfyModel(known);
                SelectedComfyModel = known;
            }
        }
        _referenceFolder = settings.ReferenceFolder ?? string.Empty;
        RaiseLocalizedPropertiesChanged();
    }

    private void RaiseLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(TitlePlaceholder));
        OnPropertyChanged(nameof(OpenText));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(ExportText));
        OnPropertyChanged(nameof(AiFixText));
        OnPropertyChanged(nameof(FocusText));
        OnPropertyChanged(nameof(FileMenuText));
        OnPropertyChanged(nameof(ViewMenuText));
        OnPropertyChanged(nameof(ThemeMenuText));
        OnPropertyChanged(nameof(ThemeDarkText));
        OnPropertyChanged(nameof(ThemeLightText));
        OnPropertyChanged(nameof(ThemeCustomText));
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(KoreanText));
        OnPropertyChanged(nameof(EnglishText));
        OnPropertyChanged(nameof(AiSetupTitleText));
        OnPropertyChanged(nameof(AiSetupDownloadText));
        OnPropertyChanged(nameof(AiSetupOpenOllamaText));
        OnPropertyChanged(nameof(AiSetupRetryText));
        OnPropertyChanged(nameof(AiSetupDismissText));
        OnPropertyChanged(nameof(AiSetupCancelText));
        OnPropertyChanged(nameof(ReferenceTitleText));
        OnPropertyChanged(nameof(ReferenceOpenFolderText));
        OnPropertyChanged(nameof(ReferenceRefreshText));
        OnPropertyChanged(nameof(ReferenceEmptyText));
        OnPropertyChanged(nameof(ChatTitleText));
        OnPropertyChanged(nameof(ChatInputHintText));
        OnPropertyChanged(nameof(ChatEmptyText));
        OnPropertyChanged(nameof(IgnoreWordText));
        OnPropertyChanged(nameof(AddToDictionaryText));
        OnPropertyChanged(nameof(NoSuggestionsText));
        OnPropertyChanged(nameof(ContextCheckText));
        OnPropertyChanged(nameof(SaveAsText));
        OnPropertyChanged(nameof(NewDocumentText));
        OnPropertyChanged(nameof(SettingsMenuText));
        OnPropertyChanged(nameof(SettingsTitleText));
        OnPropertyChanged(nameof(AutoSaveLabelText));
        OnPropertyChanged(nameof(AutoSaveIntervalLabelText));
        OnPropertyChanged(nameof(MenuFontSizeLabelText));
        OnPropertyChanged(nameof(ReferenceFontSizeLabelText));
        OnPropertyChanged(nameof(ReferenceColorLabelText));
        OnPropertyChanged(nameof(SettingsSaveText));
        OnPropertyChanged(nameof(SettingsCloseText));
        OnPropertyChanged(nameof(AiModelLabelText));
        OnPropertyChanged(nameof(ToolbarIconSizeLabelText));
        OnPropertyChanged(nameof(ChatFontSizeLabelText));
        OnPropertyChanged(nameof(ChatBackgroundLabelText));
        OnPropertyChanged(nameof(EditorFontSizeLabelText));
        OnPropertyChanged(nameof(CustomThemeLabelText));
        OnPropertyChanged(nameof(CustomBackgroundLabelText));
        OnPropertyChanged(nameof(CustomForegroundLabelText));
        OnPropertyChanged(nameof(GoalLabelText));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(WordText));
        OnPropertyChanged(nameof(CharacterText));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(ParagraphText));
        OnPropertyChanged(nameof(SentenceText));
    }
}
