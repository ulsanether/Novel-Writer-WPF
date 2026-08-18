using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 첫 실행 설치 마법사 창입니다.
/// </summary>
public partial class FirstRunSetupWindow : Window
{
    /// <summary>
    /// 마법사 창을 초기화합니다.
    /// </summary>
    /// <param name="viewModel">마법사 뷰모델입니다.</param>
    public FirstRunSetupWindow(FirstRunSetupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += Close;
    }
}
