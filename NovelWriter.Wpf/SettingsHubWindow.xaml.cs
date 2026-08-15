using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 통합 설정 창입니다. 일반·테마/외형·이미지 설정을 탭으로 합쳤습니다. DataContext로 <see cref="MainViewModel"/>를 공유합니다.
/// </summary>
public partial class SettingsHubWindow : Window
{
    /// <summary>일반 탭입니다.</summary>
    public const int TabGeneral = 0;

    /// <summary>테마·외형 탭입니다.</summary>
    public const int TabTheme = 1;

    /// <summary>이미지 탭입니다.</summary>
    public const int TabImage = 2;

    /// <summary>
    /// 통합 설정 창을 초기화합니다.
    /// </summary>
    /// <param name="viewModel">공유 뷰모델입니다.</param>
    /// <param name="initialTab">처음 표시할 탭 인덱스입니다.</param>
    public SettingsHubWindow(MainViewModel viewModel, int initialTab = TabGeneral)
    {
        InitializeComponent();
        DataContext = viewModel;
        Tabs.SelectedIndex = initialTab;

        // 일반 탭의 모델 배지 표시를 위해 설치된 모델 목록을 조회합니다.
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

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
