using System.Diagnostics;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 로컬 텍스트 LLM 실행기(Ollama)의 설치·실행을 돕습니다. (초보자용, winget 자동 설치)
/// </summary>
public sealed class OllamaSetupService
{
    /// <summary>winget(앱 설치 관리자)을 사용할 수 있는지 확인합니다.</summary>
    public bool IsWingetAvailable() => TryRun("winget", "--version");

    /// <summary>Ollama가 설치되어 있는지 확인합니다. (ollama --version)</summary>
    public bool IsInstalled() => TryRun("ollama", "--version");

    /// <summary>
    /// Ollama를 winget으로 설치합니다.
    /// </summary>
    public async Task<bool> InstallAsync(IProgress<string> progress)
    {
        if (IsInstalled())
        {
            progress.Report("Ollama가 이미 설치되어 있습니다.");
            return true;
        }

        if (!IsWingetAvailable())
        {
            progress.Report("자동 설치기(winget)가 없습니다. https://ollama.com/download 에서 직접 설치해 주세요.");
            return false;
        }

        progress.Report("Ollama를 설치하는 중입니다... (수 분 소요, 관리자 권한 창이 뜰 수 있습니다)");
        await RunAsync("winget",
            "install -e --id Ollama.Ollama --accept-source-agreements --accept-package-agreements",
            progress).ConfigureAwait(false);

        var ok = IsInstalled();
        progress.Report(ok ? "Ollama 설치 완료." : "설치를 확인하지 못했습니다. 프로그램을 다시 시작한 뒤 재시도해 주세요.");
        return ok;
    }

    /// <summary>
    /// Ollama 백그라운드 서버를 실행합니다. (이미 실행 중이면 무시)
    /// </summary>
    public void TryStartServer()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ollama", "serve")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch
        {
            // 무시 (Ollama 앱이 트레이에서 자동 실행되는 경우가 많음)
        }
    }

    private static bool TryRun(string file, string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunAsync(string file, string args, IProgress<string> progress)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(file, args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) progress.Report(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) progress.Report(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            progress.Report("오류: " + ex.Message);
            return false;
        }
    }
}
