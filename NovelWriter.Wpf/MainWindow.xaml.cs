using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NovelWriter.Wpf.Models;
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
    private SpellingAdorner? _spellingAdorner;
    private int _rightClickIndex = -1;
    private bool _syncingEditor;
    private readonly StoryProjectService _storyProjectService;
    private readonly StoryPlannerService _storyPlannerService;
    private readonly ChatService _chatService;

    /// <summary>
    /// 메인 윈도우를 초기화합니다.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovelWriter");
        var repository = new DocumentRepository(appData);
        var backupService = new BackupService(Path.Combine(appData, "Backups"));
        var documentImportService = new DocumentImportService();
        var docxExportService = new DocxExportService();
        var typoCorrectionService = new TypoCorrectionService();
        var ollamaService = new OllamaService();
        _chatService = new ChatService();
        var chatService = _chatService;
        var hunspellService = new HunspellSpellCheckService();
        var userDictionaryService = new UserDictionaryService(appData);
        var referenceLibraryService = new ReferenceLibraryService();
        _storyProjectService = new StoryProjectService(appData);
        _storyPlannerService = new StoryPlannerService(chatService);
        var statisticsService = new StatisticsService();
        var localizationService = new LocalizationService();
        var themeService = new ThemeService();
        var settingsService = new SettingsService(appData);

        _viewModel = new MainViewModel(
            repository,
            backupService,
            documentImportService,
            docxExportService,
            typoCorrectionService,
            ollamaService,
            chatService,
            hunspellService,
            userDictionaryService,
            referenceLibraryService,
            statisticsService,
            localizationService,
            themeService,
            settingsService);

        _viewModel.ImportPathResolver = ResolveImportPathAsync;
        _viewModel.ExportPathResolver = ResolveExportPathAsync;
        _viewModel.SaveAsPathResolver = ResolveSaveAsPathAsync;
        _viewModel.ReferenceFolderResolver = ResolveReferenceFolderAsync;
        _viewModel.BackgroundImageResolver = () =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = "이미지 (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|모든 파일 (*.*)|*.*"
            };
            return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
        };
        _viewModel.DocxDocumentSaver = async path =>
        {
            try
            {
                await docxExportService.ExportFlowDocumentAsync(path, _viewModel.Title, EditorTextBox.Document, _viewModel.EditorFontSize);
                return true;
            }
            catch
            {
                return false;
            }
        };
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
            SetupSpellChecking();
            await _viewModel.InitializeAsync();
        };
    }

    /// <summary>
    /// 오타 물결선 어도너와 현재 페이지 범위/우클릭 교정을 연결합니다.
    /// </summary>
    private void SetupSpellChecking()
    {
        var layer = AdornerLayer.GetAdornerLayer(EditorTextBox);
        if (layer is null)
        {
            return;
        }

        _spellingAdorner = new SpellingAdorner(EditorTextBox, () => _viewModel.TypoMarks);
        layer.Add(_spellingAdorner);

        InitFormattingToolbar();

        _viewModel.TypoMarks.CollectionChanged += (_, _) => _spellingAdorner.InvalidateVisual();
        EditorTextBox.TextChanged += EditorOnTextChanged;
        EditorTextBox.SizeChanged += (_, _) => _spellingAdorner.InvalidateVisual();
        EditorTextBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            new ScrollChangedEventHandler((_, _) =>
            {
                _spellingAdorner.InvalidateVisual();
                _viewModel.RequestSpellCheck();
            }));

        _viewModel.VisibleRangeResolver = ResolveVisibleRange;

        EditorTextBox.PreviewMouseRightButtonDown += EditorOnRightButtonDown;
        EditorTextBox.ContextMenuOpening += EditorOnContextMenuOpening;

        // 초기 Content를 에디터에 반영합니다.
        SyncEditorFromViewModel();

        // 새 채팅 메시지가 추가되면 맨 아래로 스크롤합니다.
        _viewModel.ChatMessages.CollectionChanged += (_, _) => ChatScrollViewer.ScrollToEnd();
    }

    private void EditorOnTextChanged(object sender, TextChangedEventArgs e)
    {
        _spellingAdorner?.InvalidateVisual();

        if (_syncingEditor)
        {
            return;
        }

        _syncingEditor = true;
        _viewModel.Content = RichTextBoxHelpers.GetPlainText(EditorTextBox);
        _syncingEditor = false;
    }

    /// <summary>
    /// ViewModel의 Content를 에디터 문서로 반영합니다. (로드/새 문서/오타 교체/삽입 시)
    /// </summary>
    private void SyncEditorFromViewModel()
    {
        if (_syncingEditor)
        {
            return;
        }

        if (RichTextBoxHelpers.GetPlainText(EditorTextBox) == (_viewModel.Content ?? string.Empty))
        {
            return;
        }

        _syncingEditor = true;
        RichTextBoxHelpers.SetPlainText(EditorTextBox, _viewModel.Content ?? string.Empty);
        _syncingEditor = false;
        _spellingAdorner?.InvalidateVisual();
    }

    /// <summary>
    /// 현재 화면에 보이는 텍스트 범위(시작 인덱스, 길이)를 계산합니다.
    /// </summary>
    private (int Start, int Length)? ResolveVisibleRange()
    {
        var top = EditorTextBox.GetPositionFromPoint(new Point(0, 0), true);
        var bottom = EditorTextBox.GetPositionFromPoint(
            new Point(EditorTextBox.ViewportWidth, EditorTextBox.ViewportHeight), true);
        if (top is null || bottom is null)
        {
            return null;
        }

        var start = RichTextBoxHelpers.GetOffset(EditorTextBox, top);
        var end = RichTextBoxHelpers.GetOffset(EditorTextBox, bottom);
        return end <= start ? null : (start, end - start);
    }

    private void EditorOnRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pointer = EditorTextBox.GetPositionFromPoint(e.GetPosition(EditorTextBox), true);
        _rightClickIndex = pointer is null ? -1 : RichTextBoxHelpers.GetOffset(EditorTextBox, pointer);
    }

    private void EditorOnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var mark = FindMarkAt(_rightClickIndex);
        if (mark is null)
        {
            // 오타가 아닌 곳은 기본 컨텍스트 메뉴(복사/붙여넣기)를 복원합니다.
            EditorTextBox.ClearValue(FrameworkElement.ContextMenuProperty);
            return;
        }

        var menu = new ContextMenu();

        var suggestions = _viewModel.GetSuggestions(mark);
        if (suggestions.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = _viewModel.NoSuggestionsText, IsEnabled = false });
        }
        else
        {
            foreach (var suggestion in suggestions)
            {
                var replacement = suggestion;
                var item = new MenuItem { Header = suggestion, FontWeight = FontWeights.SemiBold };
                item.Click += (_, _) => ApplyReplacementPreservingScroll(mark, replacement);
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new Separator());

        var ignoreItem = new MenuItem { Header = _viewModel.IgnoreWordText };
        ignoreItem.Click += (_, _) =>
        {
            _viewModel.IgnoreWord(mark);
            _spellingAdorner?.InvalidateVisual();
        };
        menu.Items.Add(ignoreItem);

        var addItem = new MenuItem { Header = _viewModel.AddToDictionaryText };
        addItem.Click += (_, _) =>
        {
            _viewModel.AddWordToDictionary(mark);
            _spellingAdorner?.InvalidateVisual();
        };
        menu.Items.Add(addItem);

        EditorTextBox.ContextMenu = menu;
    }

    /// <summary>
    /// 오타를 교체하되 현재 스크롤 위치를 유지합니다. (Content 전체 교체 시 맨 위로 튀는 것 방지)
    /// </summary>
    private void ApplyReplacementPreservingScroll(TypoMark mark, string replacement)
    {
        var verticalOffset = EditorTextBox.VerticalOffset;
        var horizontalOffset = EditorTextBox.HorizontalOffset;

        if (!_viewModel.ApplyReplacement(mark, replacement))
        {
            return;
        }

        // Content 교체 후 레이아웃이 갱신되면 스크롤 위치를 복원합니다.
        Dispatcher.BeginInvoke(
            new Action(() =>
            {
                EditorTextBox.ScrollToVerticalOffset(verticalOffset);
                EditorTextBox.ScrollToHorizontalOffset(horizontalOffset);
                _spellingAdorner?.InvalidateVisual();
            }),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private static string MakeSafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }

    private TypoMark? FindMarkAt(int index)
    {
        if (index < 0)
        {
            return null;
        }

        foreach (var mark in _viewModel.TypoMarks)
        {
            if (index >= mark.Start && index <= mark.End)
            {
                return mark;
            }
        }

        return null;
    }

    private Task<string?> ResolveImportPathAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "지원 문서 (*.txt;*.md;*.docx)|*.txt;*.md;*.docx|텍스트 파일 (*.txt)|*.txt|Markdown (*.md)|*.md|Word 문서 (*.docx)|*.docx|모든 파일 (*.*)|*.*"
        };

        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
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

    private Task<string?> ResolveSaveAsPathAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "텍스트 파일 (*.txt)|*.txt|Markdown (*.md)|*.md|Word 문서 (*.docx)|*.docx",
            FileName = string.IsNullOrWhiteSpace(_viewModel.Title) ? "novel" : _viewModel.Title
        };

        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_viewModel) { Owner = this };
        window.ShowDialog();
    }

    private void OnOpenReferenceGenerator(object sender, RoutedEventArgs e)
    {
        var viewModel = new ReferenceGeneratorViewModel(_chatService)
        {
            SavePathResolver = (suggested, subFolder) =>
            {
                // 기본 저장 위치를 참고자료 폴더의 유형별 하위 폴더로 (폴더로 자동 분류)
                var folder = _viewModel.ReferenceFolderPath;
                var targetFolder = folder;
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) && !string.IsNullOrWhiteSpace(subFolder))
                {
                    targetFolder = Path.Combine(folder, subFolder);
                    Directory.CreateDirectory(targetFolder);
                }

                // 넘버링 접두: 해당 폴더의 기존 .md 개수로 순번(0000_)을 매깁니다.
                var sequence = !string.IsNullOrWhiteSpace(targetFolder) && Directory.Exists(targetFolder)
                    ? Directory.GetFiles(targetFolder, "*.md").Length
                    : 0;
                var safeBase = MakeSafeFileName(suggested);
                var fileName = $"{sequence:0000}_{safeBase}";

                var dialog = new SaveFileDialog
                {
                    Filter = "Markdown (*.md)|*.md",
                    DefaultExt = ".md",
                    FileName = fileName
                };
                if (!string.IsNullOrWhiteSpace(targetFolder) && Directory.Exists(targetFolder))
                {
                    dialog.InitialDirectory = targetFolder;
                }

                return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
            }
        };

        var window = new ReferenceGeneratorWindow(viewModel) { Owner = this };
        window.ShowDialog();

        // 저장된 .md가 참고자료 폴더에 있으면 서랍을 새로고침합니다.
        _viewModel.RefreshReferencesCommand.Execute(null);
    }

    private void OnOpenThemeCustom(object sender, RoutedEventArgs e)
    {
        var window = new ThemeCustomWindow(_viewModel) { Owner = this };
        window.ShowDialog();
    }

    private void OnOpenStoryPlanner(object sender, RoutedEventArgs e)
    {
        var project = _storyProjectService.Load();
        var viewModel = new StoryPlannerViewModel(
            project, _storyProjectService, _storyPlannerService,
            new ReferenceLibraryService(), _viewModel.ReferenceFolderPath)
        {
            InsertToEditor = text =>
            {
                _viewModel.Content = string.IsNullOrEmpty(_viewModel.Content)
                    ? text
                    : _viewModel.Content + "\n\n" + text;
            },
            GetManuscript = () => _viewModel.Content ?? string.Empty
        };

        var window = new StoryPlannerWindow(viewModel) { Owner = this };
        window.ShowDialog();
    }

    private Task<string?> ResolveReferenceFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "참고자료 폴더 선택"
        };

        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FolderName : null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Content))
        {
            SyncEditorFromViewModel();
            return;
        }

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

    // ────────────────────────── 서식 툴바 ──────────────────────────

    private void InitFormattingToolbar()
    {
        FontFamilyCombo.ItemsSource = new[]
        {
            "맑은 고딕", "바탕", "굴림", "돋움", "궁서", "나눔고딕", "나눔명조", "Consolas", "Segoe UI", "Times New Roman"
        };
        FontSizeCombo.ItemsSource = new[]
        {
            "10", "11", "12", "14", "16", "18", "20", "24", "28", "32", "36", "40", "48", "60", "72"
        };
    }

    private void OnFontFamilyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontFamilyCombo.SelectedItem is string name && EditorTextBox is not null)
        {
            EditorTextBox.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, new FontFamily(name));
            EditorTextBox.Focus();
        }
    }

    private void OnFontSizeSelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFontSize();

    private void OnFontSizeChanged(object sender, RoutedEventArgs e) => ApplyFontSize();

    private void ApplyFontSize()
    {
        if (EditorTextBox is not null && double.TryParse(FontSizeCombo.Text, out var size) && size > 0)
        {
            EditorTextBox.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        }
    }

    private void OnBold(object sender, RoutedEventArgs e)
    {
        var current = EditorTextBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
        var isBold = current is FontWeight weight && weight == FontWeights.Bold;
        EditorTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, isBold ? FontWeights.Normal : FontWeights.Bold);
        EditorTextBox.Focus();
    }

    private void OnItalic(object sender, RoutedEventArgs e)
    {
        var current = EditorTextBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
        var isItalic = current is FontStyle style && style == FontStyles.Italic;
        EditorTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, isItalic ? FontStyles.Normal : FontStyles.Italic);
        EditorTextBox.Focus();
    }

    private void OnUnderline(object sender, RoutedEventArgs e)
    {
        var current = EditorTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var hasUnderline = current is TextDecorationCollection decorations && decorations.Count > 0;
        EditorTextBox.Selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            hasUnderline ? null : TextDecorations.Underline);
        EditorTextBox.Focus();
    }

    private void OnForegroundColor(object sender, RoutedEventArgs e)
    {
        ShowColorMenu((FrameworkElement)sender, color =>
            EditorTextBox.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(color)));
    }

    private void OnHighlightColor(object sender, RoutedEventArgs e)
    {
        ShowColorMenu((FrameworkElement)sender, color =>
            EditorTextBox.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(color)));
    }

    private void OnClearFormatting(object sender, RoutedEventArgs e)
    {
        var selection = EditorTextBox.Selection;
        selection.ClearAllProperties();
        selection.ApplyPropertyValue(TextElement.BackgroundProperty, null);
        EditorTextBox.Focus();
    }

    private void ShowColorMenu(FrameworkElement target, Action<Color> onPick)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };

        foreach (var hex in _viewModel.PaletteColors)
        {
            Color color;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                continue;
            }

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var item = new MenuItem
            {
                Header = hex,
                Icon = new System.Windows.Shapes.Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = brush,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 1
                }
            };
            var picked = color;
            item.Click += (_, _) =>
            {
                onPick(picked);
                EditorTextBox.Focus();
            };
            menu.Items.Add(item);
        }

        menu.IsOpen = true;
    }
}