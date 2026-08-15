using System.Diagnostics;
using System.IO;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 이미지 생성 서버(AUTOMATIC1111 SD WebUI)의 설치·설정·실행을 돕습니다. (초보자용)
/// Python·git이 전혀 없는 사용자를 위해 winget으로 사전 요구 프로그램까지 자동 설치합니다.
/// </summary>
public sealed class ImageSetupService
{
    private const string RepoUrl = "https://github.com/AUTOMATIC1111/stable-diffusion-webui.git";

    /// <summary>AUTOMATIC1111이 요구하는 파이썬 마이너 버전들입니다. (3.10 권장, 3.11까지 허용)</summary>
    private static readonly int[] CompatiblePythonMinors = { 10, 11 };

    /// <summary>
    /// git 명령을 사용할 수 있는지 확인합니다.
    /// </summary>
    public bool IsGitAvailable() => TryRun("git", "--version");

    /// <summary>
    /// winget(앱 설치 관리자)을 사용할 수 있는지 확인합니다.
    /// </summary>
    public bool IsWingetAvailable() => TryRun("winget", "--version");

    /// <summary>
    /// 지정한 폴더에 WebUI가 설치되어 있는지 확인합니다.
    /// </summary>
    public bool IsInstalled(string? directory)
        => !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, "webui-user.bat"));

    /// <summary>
    /// AUTOMATIC1111과 호환되는 파이썬(3.10/3.11) 실행 경로를 찾습니다. 없으면 null입니다.
    /// </summary>
    public string? FindCompatiblePython()
    {
        var candidates = new List<string>();

        // 잘 알려진 설치 위치들
        var localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
        foreach (var minor in CompatiblePythonMinors)
        {
            candidates.Add($@"C:\Program Files\Python3{minor}\python.exe");
            candidates.Add($@"C:\Python3{minor}\python.exe");
            candidates.Add(Path.Combine(localPrograms, $"Python3{minor}", "python.exe"));
        }

        // py 런처가 알고 있는 경로들
        foreach (var minor in CompatiblePythonMinors)
        {
            var byLauncher = CaptureRun("py", $"-3.{minor} -c \"import sys;print(sys.executable)\"");
            if (!string.IsNullOrWhiteSpace(byLauncher) && File.Exists(byLauncher.Trim()))
            {
                candidates.Add(byLauncher.Trim());
            }
        }

        foreach (var path in candidates.Distinct())
        {
            if (File.Exists(path) && IsCompatiblePythonExe(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// 호환 파이썬을 확보합니다. 없으면 winget으로 Python 3.10을 설치한 뒤 경로를 반환합니다.
    /// </summary>
    /// <returns>호환 파이썬 경로. 확보 실패 시 null입니다.</returns>
    public async Task<string?> EnsurePythonAsync(IProgress<string> progress)
    {
        var found = FindCompatiblePython();
        if (found is not null)
        {
            progress.Report($"호환 파이썬을 찾았습니다: {found}");
            return found;
        }

        if (!IsWingetAvailable())
        {
            progress.Report("Python 3.10이 없고 자동 설치기(winget)도 없습니다. https://www.python.org/downloads/release/python-31011/ 에서 Python 3.10을 설치해 주세요.");
            return null;
        }

        progress.Report("Python 3.10을 설치하는 중입니다... (수 분 소요, 관리자 권한 창이 뜰 수 있습니다)");
        await RunAsync("winget",
            "install -e --id Python.Python.3.10 --scope machine --accept-source-agreements --accept-package-agreements",
            null, progress).ConfigureAwait(false);

        // 설치 직후 PATH 갱신 전이라 재탐색으로 경로를 확보
        found = FindCompatiblePython();
        if (found is null)
        {
            progress.Report("Python 설치를 확인하지 못했습니다. 프로그램을 다시 시작한 뒤 재시도해 주세요.");
        }
        else
        {
            progress.Report($"Python 3.10 설치 완료: {found}");
        }

        return found;
    }

    /// <summary>
    /// git을 확보합니다. 없으면 winget으로 설치합니다.
    /// </summary>
    /// <returns>git 사용 가능 여부입니다.</returns>
    public async Task<bool> EnsureGitAsync(IProgress<string> progress)
    {
        if (IsGitAvailable())
        {
            return true;
        }

        if (!IsWingetAvailable())
        {
            progress.Report("git이 없고 자동 설치기(winget)도 없습니다. https://git-scm.com 에서 git을 설치해 주세요.");
            return false;
        }

        progress.Report("git을 설치하는 중입니다... (수 분 소요)");
        await RunAsync("winget",
            "install -e --id Git.Git --scope machine --accept-source-agreements --accept-package-agreements",
            null, progress).ConfigureAwait(false);

        var ok = IsGitAvailable();
        progress.Report(ok ? "git 설치 완료." : "git 설치를 확인하지 못했습니다. 프로그램을 다시 시작한 뒤 재시도해 주세요.");
        return ok;
    }

    /// <summary>
    /// WebUI를 git으로 내려받고, 호환 파이썬 지정 + --api 옵션을 설정합니다.
    /// Python·git이 없으면 winget으로 함께 설치합니다.
    /// </summary>
    /// <param name="targetDirectory">설치할 폴더입니다.</param>
    /// <param name="progress">진행 로그입니다.</param>
    /// <returns>성공 여부입니다.</returns>
    public async Task<bool> InstallAsync(string targetDirectory, IProgress<string> progress)
    {
        // 1) git 확보 (clone에 필요)
        if (!await EnsureGitAsync(progress).ConfigureAwait(false))
        {
            return false;
        }

        // 2) 호환 파이썬 확보 (없으면 3.10 자동 설치)
        var python = await EnsurePythonAsync(progress).ConfigureAwait(false);

        var repoDir = Path.Combine(targetDirectory, "stable-diffusion-webui");
        if (IsInstalled(repoDir))
        {
            progress.Report("이미 내려받아져 있습니다. 설정만 확인합니다.");
        }
        else
        {
            progress.Report("WebUI를 내려받는 중입니다... (수 분 소요)");
            Directory.CreateDirectory(targetDirectory);
            var ok = await RunAsync("git", $"clone --depth 1 {RepoUrl} \"{repoDir}\"", targetDirectory, progress).ConfigureAwait(false);
            if (!ok)
            {
                progress.Report("내려받기에 실패했습니다.");
                return false;
            }
        }

        // 3) 호환 파이썬을 webui-user.bat에 지정 (없으면 건너뜀 → 기본 python 사용)
        if (python is not null)
        {
            SetPythonInBat(repoDir, python);
            RemoveIncompatibleVenv(repoDir, python, progress);
        }

        // 4) 최신 setuptools(pkg_resources 제거) 문제 방어 — 빌드 격리에도 옛 setuptools 강제
        EnsurePipConstraints(repoDir);
        EnsureApiFlag(repoDir);
        progress.Report("설치가 준비되었습니다. [WebUI 실행]을 누르면 첫 실행 시 필요한 파일을 자동으로 받습니다(수 GB · 시간 소요).");
        return true;
    }

    /// <summary>
    /// webui-user.bat의 <c>set PYTHON=</c>에 지정한 파이썬 경로를 씁니다.
    /// </summary>
    public void SetPythonInBat(string directory, string pythonExe)
    {
        try
        {
            var batPath = Path.Combine(directory, "webui-user.bat");
            if (!File.Exists(batPath))
            {
                return;
            }

            var quoted = $"set PYTHON=\"{pythonExe}\"";
            var lines = File.ReadAllLines(batPath).ToList();
            var idx = lines.FindIndex(l => l.TrimStart().StartsWith("set PYTHON=", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                lines[idx] = quoted;
            }
            else
            {
                lines.Insert(0, quoted);
            }

            File.WriteAllLines(batPath, lines);
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// webui-user.bat에 --api 플래그를 추가합니다. (앱에서 API로 호출하기 위함)
    /// </summary>
    public void EnsureApiFlag(string directory)
    {
        try
        {
            var batPath = Path.Combine(directory, "webui-user.bat");
            if (!File.Exists(batPath))
            {
                return;
            }

            var lines = File.ReadAllLines(batPath).ToList();
            var idx = lines.FindIndex(l => l.TrimStart().StartsWith("set COMMANDLINE_ARGS=", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                if (!lines[idx].Contains("--api", StringComparison.OrdinalIgnoreCase))
                {
                    lines[idx] = lines[idx].TrimEnd() + " --api";
                }
            }
            else
            {
                lines.Add("set COMMANDLINE_ARGS=--api");
            }

            File.WriteAllLines(batPath, lines);
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// 기존 venv가 호환되지 않는 파이썬으로 만들어졌으면 삭제해 재생성을 유도합니다.
    /// </summary>
    private void RemoveIncompatibleVenv(string directory, string expectedPython, IProgress<string> progress)
    {
        try
        {
            var venvDir = Path.Combine(directory, "venv");
            var cfg = Path.Combine(venvDir, "pyvenv.cfg");
            if (!File.Exists(cfg))
            {
                return;
            }

            var text = File.ReadAllText(cfg);
            var compatible = CompatiblePythonMinors.Any(m => text.Contains($"3.{m}"));
            if (!compatible)
            {
                progress.Report("이전에 호환되지 않는 파이썬으로 만들어진 venv를 정리합니다.");
                Directory.Delete(venvDir, recursive: true);
            }
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// 최신 setuptools(81+)가 <c>pkg_resources</c>를 제거해 CLIP/gfpgan 등 옛 패키지 빌드가 깨지는 문제를 방어합니다.
    /// <c>pip-constraints.txt</c>(setuptools&lt;70)를 만들고 webui-user.bat에 <c>set PIP_CONSTRAINT=</c>를 지정해
    /// pip 빌드 격리 환경에도 옛 setuptools가 쓰이도록 강제합니다.
    /// </summary>
    public void EnsurePipConstraints(string directory)
    {
        try
        {
            var constraintsPath = Path.Combine(directory, "pip-constraints.txt");
            File.WriteAllText(constraintsPath, "setuptools<70\nwheel\n");

            var batPath = Path.Combine(directory, "webui-user.bat");
            if (!File.Exists(batPath))
            {
                return;
            }

            var line = $"set PIP_CONSTRAINT={constraintsPath}";
            var lines = File.ReadAllLines(batPath).ToList();
            var idx = lines.FindIndex(l => l.TrimStart().StartsWith("set PIP_CONSTRAINT=", StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                lines[idx] = line;
            }
            else
            {
                // call webui.bat 앞에 삽입(없으면 맨 끝)
                var callIdx = lines.FindIndex(l => l.TrimStart().StartsWith("call webui.bat", StringComparison.OrdinalIgnoreCase));
                if (callIdx >= 0)
                {
                    lines.Insert(callIdx, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            File.WriteAllLines(batPath, lines);
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// webui-user.bat의 COMMANDLINE_ARGS에 하드웨어 인자(예 --medvram/--lowvram)를 보장합니다.
    /// 없는 토큰만 추가하고 --api는 유지합니다.
    /// </summary>
    public void EnsureExtraArgs(string directory, string extraArgs)
    {
        if (string.IsNullOrWhiteSpace(extraArgs))
        {
            return;
        }

        try
        {
            var batPath = Path.Combine(directory, "webui-user.bat");
            if (!File.Exists(batPath))
            {
                return;
            }

            var lines = File.ReadAllLines(batPath).ToList();
            var idx = lines.FindIndex(l => l.TrimStart().StartsWith("set COMMANDLINE_ARGS=", StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
            {
                lines.Add("set COMMANDLINE_ARGS=" + extraArgs);
                File.WriteAllLines(batPath, lines);
                return;
            }

            var line = lines[idx];
            foreach (var token in extraArgs.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!line.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    line = line.TrimEnd() + " " + token;
                }
            }

            lines[idx] = line;
            File.WriteAllLines(batPath, lines);
        }
        catch
        {
            // 무시
        }
    }

    /// <summary>
    /// WebUI(webui-user.bat)를 실행합니다.
    /// </summary>
    public bool Launch(string directory)
    {
        try
        {
            var batPath = Path.Combine(directory, "webui-user.bat");
            if (!File.Exists(batPath))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(batPath)
            {
                UseShellExecute = true,
                WorkingDirectory = directory
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>지정한 파이썬 실행 파일의 버전이 호환(3.10/3.11)되는지 확인합니다.</summary>
    private static bool IsCompatiblePythonExe(string pythonExe)
    {
        var output = CaptureRun(pythonExe, "--version");
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        // "Python 3.10.11" 형태
        return CompatiblePythonMinors.Any(m => output.Contains($"3.{m}."));
    }

    /// <summary>프로세스를 실행하고 종료 코드가 0인지만 확인합니다. (존재 여부 확인용)</summary>
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

    /// <summary>프로세스를 실행하고 표준 출력을 문자열로 반환합니다. 실패 시 null입니다.</summary>
    private static string? CaptureRun(string file, string args)
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
                return null;
            }

            var output = process.StandardOutput.ReadToEnd();
            var err = process.StandardError.ReadToEnd();
            process.WaitForExit(8000);
            var combined = string.IsNullOrWhiteSpace(output) ? err : output;
            return string.IsNullOrWhiteSpace(combined) ? null : combined.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> RunAsync(string file, string args, string? workingDir, IProgress<string> progress)
    {
        try
        {
            var startInfo = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                startInfo.WorkingDirectory = workingDir;
            }

            using var process = new Process { StartInfo = startInfo };
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
