using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 설정 창입니다. DataContext로 <see cref="MainViewModel"/>를 공유합니다.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>
    /// 설정 창을 초기화합니다.
    /// </summary>
    /// <param name="viewModel">공유 뷰모델입니다.</param>
    public SettingsWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 설정 창을 열 때만 설치된 모델 목록을 조회합니다. (배지 표시용)
        _ = viewModel.RefreshInstalledModelsAsync();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.SaveSettingsCommand.Execute(null);
        }

        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
