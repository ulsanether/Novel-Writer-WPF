using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 참고자료(.md) 생성기 창입니다.
/// </summary>
public partial class ReferenceGeneratorWindow : Window
{
    /// <summary>
    /// 창을 초기화합니다.
    /// </summary>
    public ReferenceGeneratorWindow(ReferenceGeneratorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
