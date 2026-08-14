using System.Windows;
using NovelWriter.Wpf.ViewModels;

namespace NovelWriter.Wpf;

/// <summary>
/// 테마 커스텀 창입니다. DataContext로 <see cref="MainViewModel"/>를 공유합니다.
/// </summary>
public partial class ThemeCustomWindow : Window
{
    /// <summary>
    /// 창을 초기화합니다.
    /// </summary>
    public ThemeCustomWindow(MainViewModel viewModel)
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
