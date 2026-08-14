using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 스토리 플래너 창입니다.
/// </summary>
public partial class StoryPlannerWindow : Window
{
    private const string ProjectFilter = "스토리 프로젝트 (*.json)|*.json|모든 파일 (*.*)|*.*";
    private readonly StoryPlannerViewModel _viewModel;

    /// <summary>
    /// 창을 초기화합니다.
    /// </summary>
    public StoryPlannerWindow(StoryPlannerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.SaveAsPathResolver = ResolveSaveAsPathAsync;
        _viewModel.OpenPathResolver = ResolveOpenPathAsync;
        _viewModel.ConfirmOverwrite = message =>
            MessageBox.Show(this, message, "덮어쓰기 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
        DataContext = viewModel;
    }

    private Task<string?> ResolveSaveAsPathAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = ProjectFilter,
            DefaultExt = ".json",
            FileName = string.IsNullOrWhiteSpace(_viewModel.Project.Title) ? "story" : _viewModel.Project.Title
        };

        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
    }

    private Task<string?> ResolveOpenPathAsync()
    {
        var dialog = new OpenFileDialog { Filter = ProjectFilter };
        return Task.FromResult(dialog.ShowDialog(this) == true ? dialog.FileName : null);
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        _viewModel.Select(e.NewValue);
    }
}
