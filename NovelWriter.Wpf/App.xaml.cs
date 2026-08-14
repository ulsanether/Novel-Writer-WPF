using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace NovelWriter.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 앱을 초기화하고 전역 예외 핸들러를 등록합니다.
    /// </summary>
    public App()
    {
        // UI 스레드 예외로 앱이 강제 종료되지 않도록 잡아서 로그에 기록합니다.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NovelWriter");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "error.log"),
                $"[{DateTimeOffset.Now:O}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 로그 기록 실패는 무시합니다.
        }

        e.Handled = true;
    }
}

