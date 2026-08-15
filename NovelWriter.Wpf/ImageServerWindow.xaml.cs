using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 이미지 생성 서버(SD WebUI) 설정·설치 창입니다. DataContext로 <see cref="MainViewModel"/>를 공유합니다.
/// </summary>
public partial class ImageServerWindow : Window
{
    /// <summary>
    /// 이미지 서버 설정 창을 초기화합니다.
    /// </summary>
    /// <param name="viewModel">공유 뷰모델입니다.</param>
    public ImageServerWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
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
