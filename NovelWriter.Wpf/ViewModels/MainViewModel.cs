using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
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
    private readonly HunspellSpellCheckService _hunspellService;
    private readonly UserDictionaryService _userDictionaryService;
    private readonly ReferenceLibraryService _referenceLibraryService;
    private readonly StatisticsService _statisticsService;
    private readonly LocalizationService _localizationService;
    private readonly ThemeService _themeService;
    private readonly SettingsService _settingsService;
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
        HunspellSpellCheckService hunspellService,
        UserDictionaryService userDictionaryService,
        ReferenceLibraryService referenceLibraryService,
        StatisticsService statisticsService,
        LocalizationService localizationService,
        ThemeService themeService,
        SettingsService settingsService)
    {
        _repository = repository;
        _backupService = backupService;
        _documentImportService = documentImportService;
        _docxExportService = docxExportService;
        _typoCorrectionService = typoCorrectionService;
        _ollamaService = ollamaService;
        _chatService = chatService;
        _hunspellService = hunspellService;
        _userDictionaryService = userDictionaryService;
        _referenceLibraryService = referenceLibraryService;
        _statisticsService = statisticsService;
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;

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
        await _repository.InitializeAsync();
        var settings = await _settingsService.LoadAsync();
        ApplySettings(settings);

        var (title, content) = await _repository.LoadLatestAsync();
        Title = title;
        Content = content;
        ApplyTheme();
        UpdateStatistics();

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
            ChatBackgroundHex = ChatBackgroundHex
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
        DailyWordGoal = settings.DailyWordGoal;
        AiModel = string.IsNullOrWhiteSpace(settings.AiModel) ? "exaone3.5:7.8b" : settings.AiModel;
        _aiBaseUrl = string.IsNullOrWhiteSpace(settings.AiBaseUrl) ? "http://localhost:11434/v1" : settings.AiBaseUrl;
        _typoCorrectionService.Model = AiModel;
        _typoCorrectionService.BaseUrl = _aiBaseUrl;
        _chatService.Model = AiModel;
        _chatService.BaseUrl = _aiBaseUrl;
        _ollamaService.BaseUrl = ToOllamaNativeUrl(_aiBaseUrl);
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
