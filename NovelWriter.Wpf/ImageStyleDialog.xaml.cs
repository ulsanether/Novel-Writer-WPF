using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 이미지 생성 전 화풍(스타일)을 설정하는 팝업 창입니다. DataContext로 <see cref="MainViewModel"/>를 공유합니다.
/// [이미지 생성]을 누르면 DialogResult=true로 닫힙니다.
/// </summary>
public partial class ImageStyleDialog : Window
{
    /// <summary>
    /// 화풍 설정 창을 초기화합니다.
    /// </summary>
    /// <param name="viewModel">공유 뷰모델입니다.</param>
    public ImageStyleDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnGenerate(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
