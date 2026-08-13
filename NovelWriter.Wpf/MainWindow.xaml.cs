using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using NovelWriter.Wpf.Services;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 메인 에디터 윈도우입니다.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _chromeHideTimer;

    /// <summary>
    /// 메인 윈도우를 초기화합니다.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovelWriter");
        var repository = new DocumentRepository(appData);
        var backupService = new BackupService(Path.Combine(appData, "Backups"));
        var docxExportService = new DocxExportService();
        var typoCorrectionService = new TypoCorrectionService();
        var statisticsService = new StatisticsService();
        var localizationService = new LocalizationService();
        var themeService = new ThemeService();
        var settingsService = new SettingsService(appData);

        _viewModel = new MainViewModel(
            repository,
            backupService,
            docxExportService,
            typoCorrectionService,
            statisticsService,
            localizationService,
            themeService,
            settingsService);

        _viewModel.ExportPathResolver = ResolveExportPathAsync;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        _chromeHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _chromeHideTimer.Tick += (_, _) =>
        {
            if (_viewModel.IsFocusMode)
            {
                ChromePanel.Visibility = Visibility.Collapsed;
            }
        };

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            _viewModel.ResetTimerCommand.Execute(null);
        };
    }

    private async Task<string?> ResolveExportPathAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Word Document (*.docx)|*.docx",
            FileName = string.IsNullOrWhiteSpace(_viewModel.Title) ? "novel" : _viewModel.Title
        };

        if (dialog.ShowDialog(this) == true)
        {
            return await Task.FromResult(dialog.FileName);
        }

        return null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsFocusMode))
        {
            return;
        }

        if (_viewModel.IsFocusMode)
        {
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Topmost = false;
            ChromePanel.Visibility = Visibility.Visible;
        }
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_viewModel.IsFocusMode)
        {
            return;
        }

        ChromePanel.Visibility = Visibility.Visible;
        _chromeHideTimer.Stop();
        _chromeHideTimer.Start();
    }
}