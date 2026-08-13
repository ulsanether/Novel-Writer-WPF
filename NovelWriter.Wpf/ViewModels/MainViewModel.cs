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
    private readonly DocxExportService _docxExportService;
    private readonly TypoCorrectionService _typoCorrectionService;
    private readonly StatisticsService _statisticsService;
    private readonly LocalizationService _localizationService;
    private readonly ThemeService _themeService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly DispatcherTimer _focusTimer;
    private TimeSpan _remainingFocusTime = TimeSpan.Zero;

    /// <summary>
    /// 파일 저장 UI를 호출하기 위한 콜백입니다.
    /// </summary>
    public Func<Task<string?>>? ExportPathResolver { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public MainViewModel(
        DocumentRepository repository,
        BackupService backupService,
        DocxExportService docxExportService,
        TypoCorrectionService typoCorrectionService,
        StatisticsService statisticsService,
        LocalizationService localizationService,
        ThemeService themeService,
        SettingsService settingsService)
    {
        _repository = repository;
        _backupService = backupService;
        _docxExportService = docxExportService;
        _typoCorrectionService = typoCorrectionService;
        _statisticsService = statisticsService;
        _localizationService = localizationService;
        _themeService = themeService;
        _settingsService = settingsService;

        _autoSaveTimer = new DispatcherTimer();
        _autoSaveTimer.Tick += async (_, _) => await AutoSaveAsync().ConfigureAwait(false);

        _focusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _focusTimer.Tick += FocusTimerOnTick;
    }

    /// <summary>
    /// ViewModel 초기 로딩을 수행합니다.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync().ConfigureAwait(false);
        var settings = await _settingsService.LoadAsync().ConfigureAwait(false);
        ApplySettings(settings);

        var (title, content) = await _repository.LoadLatestAsync().ConfigureAwait(false);
        Title = title;
        Content = content;
        ApplyTheme();
        UpdateStatistics();

        _autoSaveTimer.Interval = TimeSpan.FromSeconds(Math.Max(10, AutoSaveSeconds));
        _autoSaveTimer.Start();
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
    private int _focusMinutes = 25;

    [ObservableProperty]
    private string _remainingTimerText = "25:00";

    [ObservableProperty]
    private bool _isFocusRunning;

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
    private Brush _editorBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF121212"));

    [ObservableProperty]
    private Brush _editorForeground = new SolidColorBrush(Colors.WhiteSmoke);

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 저장 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        await _repository.SaveAsync(Title, Content).ConfigureAwait(false);
        await _backupService.CreateBackupAsync(Title, Content).ConfigureAwait(false);
        StatusMessage = _localizationService.Get(LanguageCode, "Saved");
    }

    /// <summary>
    /// 자동 저장을 실행합니다.
    /// </summary>
    private async Task AutoSaveAsync()
    {
        await _repository.SaveAsync(Title, Content).ConfigureAwait(false);
        StatusMessage = _localizationService.Get(LanguageCode, "AutoSaved");
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

        var path = await ExportPathResolver().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await _docxExportService.ExportAsync(path, Title, Content).ConfigureAwait(false);
    }

    /// <summary>
    /// AI 오타 보정을 실행합니다.
    /// </summary>
    [RelayCommand]
    private async Task FixTyposAsync()
    {
        Content = await _typoCorrectionService.CorrectAsync(Content).ConfigureAwait(false);
        StatusMessage = _localizationService.Get(LanguageCode, "AiFixed");
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
        await PersistSettingsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 라이트 테마 적용 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SetLightThemeAsync()
    {
        Theme = "Light";
        ApplyTheme();
        await PersistSettingsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 커스텀 테마 적용 명령입니다.
    /// </summary>
    [RelayCommand]
    private async Task SetCustomThemeAsync()
    {
        Theme = "Custom";
        ApplyTheme();
        await PersistSettingsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 한국어로 UI 언어를 설정합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetKoreanAsync()
    {
        LanguageCode = "ko-KR";
        RaiseLocalizedPropertiesChanged();
        await PersistSettingsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 영어로 UI 언어를 설정합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetEnglishAsync()
    {
        LanguageCode = "en-US";
        RaiseLocalizedPropertiesChanged();
        await PersistSettingsAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 집중 타이머 시작/일시정지 명령입니다.
    /// </summary>
    [RelayCommand]
    private void ToggleFocusTimer()
    {
        if (!IsFocusRunning)
        {
            if (_remainingFocusTime <= TimeSpan.Zero)
            {
                _remainingFocusTime = TimeSpan.FromMinutes(Math.Max(1, FocusMinutes));
                RemainingTimerText = _remainingFocusTime.ToString(@"mm\:ss");
            }

            _focusTimer.Start();
        }
        else
        {
            _focusTimer.Stop();
        }

        IsFocusRunning = !IsFocusRunning;
    }

    /// <summary>
    /// 타이머를 초기화합니다.
    /// </summary>
    [RelayCommand]
    private void ResetTimer()
    {
        _focusTimer.Stop();
        IsFocusRunning = false;
        _remainingFocusTime = TimeSpan.FromMinutes(Math.Max(1, FocusMinutes));
        RemainingTimerText = _remainingFocusTime.ToString(@"mm\:ss");
    }

    private void FocusTimerOnTick(object? sender, EventArgs e)
    {
        _remainingFocusTime = _remainingFocusTime.Subtract(TimeSpan.FromSeconds(1));
        if (_remainingFocusTime <= TimeSpan.Zero)
        {
            _focusTimer.Stop();
            IsFocusRunning = false;
            _remainingFocusTime = TimeSpan.Zero;
        }

        RemainingTimerText = _remainingFocusTime.ToString(@"mm\:ss");
    }

    partial void OnContentChanged(string value)
    {
        UpdateStatistics();
    }

    partial void OnDailyWordGoalChanged(int value)
    {
        UpdateProgress();
    }

    partial void OnFocusMinutesChanged(int value)
    {
        if (!IsFocusRunning)
        {
            _remainingFocusTime = TimeSpan.FromMinutes(Math.Max(1, value));
            RemainingTimerText = _remainingFocusTime.ToString(@"mm\:ss");
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
    /// 진행률 텍스트를 반환합니다.
    /// </summary>
    public string ProgressText => _localizationService.Get(LanguageCode, "Progress");

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
            FocusMinutes = FocusMinutes
        }).ConfigureAwait(false);
    }

    private void ApplySettings(AppSettings settings)
    {
        Theme = settings.Theme;
        LanguageCode = settings.LanguageCode;
        CustomBackgroundHex = settings.CustomBackgroundHex;
        CustomForegroundHex = settings.CustomForegroundHex;
        AutoSaveSeconds = settings.AutoSaveSeconds;
        DailyWordGoal = settings.DailyWordGoal;
        FocusMinutes = settings.FocusMinutes;
        _remainingFocusTime = TimeSpan.FromMinutes(Math.Max(1, FocusMinutes));
        RemainingTimerText = _remainingFocusTime.ToString(@"mm\:ss");
        RaiseLocalizedPropertiesChanged();
    }

    private void RaiseLocalizedPropertiesChanged()
    {
        OnPropertyChanged(nameof(TitlePlaceholder));
        OnPropertyChanged(nameof(SaveText));
        OnPropertyChanged(nameof(ExportText));
        OnPropertyChanged(nameof(AiFixText));
        OnPropertyChanged(nameof(FocusText));
        OnPropertyChanged(nameof(StatsText));
        OnPropertyChanged(nameof(ProgressText));
    }
}
