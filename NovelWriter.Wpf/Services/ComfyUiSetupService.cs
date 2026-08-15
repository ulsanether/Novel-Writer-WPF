using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// ComfyUI 포터블(embedded Python) 빌드의 다운로드·압축 해제·실행을 돕습니다. (초보자용)
/// A1111과 달리 Python/venv/torch 설치가 필요 없어 파이썬 버전 문제가 생기지 않습니다.
/// </summary>
public sealed class ComfyUiSetupService
{
    // NVIDIA용 포터블 최신 릴리스 (.7z, 약 1.5~2GB). embedded Python 포함.
    private const string PortableUrl = "https://github.com/comfyanonymous/ComfyUI/releases/latest/download/ComfyUI_windows_portable_nvidia.7z";

    private static readonly HttpClient HttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// 지정 폴더에 ComfyUI 포터블이 설치되어 있는지 확인합니다.
    /// </summary>
    public bool IsInstalled(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        return File.Exists(Path.Combine(directory, "run_nvidia_gpu.bat"))
            || File.Exists(Path.Combine(directory, "ComfyUI_windows_portable", "run_nvidia_gpu.bat"));
    }

    /// <summary>
    /// 설치 폴더에서 실제 실행 폴더(run_*.bat가 있는 곳)를 찾습니다.
    /// </summary>
    public string? ResolveRunDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        if (File.Exists(Path.Combine(directory, "run_nvidia_gpu.bat")))
        {
            return directory;
        }

        var nested = Path.Combine(directory, "ComfyUI_windows_portable");
        return File.Exists(Path.Combine(nested, "run_nvidia_gpu.bat")) ? nested : null;
    }

    /// <summary>
    /// ComfyUI 포터블을 내려받아 압축을 풉니다. (7-Zip 없으면 winget으로 자동 설치)
    /// </summary>
    /// <param name="targetDirectory">설치할 폴더입니다.</param>
    /// <param name="progress">진행 로그입니다.</param>
    /// <returns>성공 여부입니다.</returns>
    public async Task<bool> InstallAsync(string targetDirectory, IProgress<string> progress)
    {
        if (IsInstalled(targetDirectory))
        {
            progress.Report("이미 설치되어 있습니다.");
            return true;
        }

        // 1) 7-Zip 확보 (.7z 해제에 필요)
        var sevenZip = await EnsureSevenZipAsync(progress).ConfigureAwait(false);
        if (sevenZip is null)
        {
            return false;
        }

        Directory.CreateDirectory(targetDirectory);
        var archivePath = Path.Combine(targetDirectory, "ComfyUI_portable.7z");

        // 2) 다운로드 (대용량)
        progress.Report("ComfyUI 포터블을 내려받는 중입니다... (약 1.5~2GB, 회선에 따라 시간 소요)");
        if (!await DownloadAsync(PortableUrl, archivePath, progress).ConfigureAwait(false))
        {
            progress.Report("다운로드에 실패했습니다. 인터넷 연결을 확인하거나 릴리스 페이지에서 직접 받아주세요.");
            return false;
        }

        // 3) 압축 해제
        progress.Report("압축을 푸는 중입니다...");
        var ok = await RunAsync(sevenZip, $"x \"{archivePath}\" -o\"{targetDirectory}\" -y", targetDirectory, progress).ConfigureAwait(false);
        if (!ok)
        {
            progress.Report("압축 해제에 실패했습니다.");
            return false;
        }

        try { File.Delete(archivePath); } catch { /* 무시 */ }

        progress.Report("설치 완료. ⚠️ ComfyUI에는 이미지 모델(체크포인트)이 포함되어 있지 않습니다.");
        progress.Report("SDXL 등 .safetensors 모델을 내려받아 [ComfyUI]\\models\\checkpoints 폴더에 넣은 뒤 [실행]하세요.");
        progress.Report("추천(8GB): SDXL 기반 모델(예: Illustrious/Pony/animagine 계열) 또는 SDXL Base 1.0.");
        return true;
    }

    /// <summary>
    /// ComfyUI를 실행합니다. 하드웨어 인자(<paramref name="extraArgs"/>: 예 <c>--lowvram</c>, <c>--cpu</c>)를 반영합니다.
    /// </summary>
    /// <param name="directory">설치 폴더입니다.</param>
    /// <param name="extraArgs">추가 실행 인자입니다. (하드웨어 프로파일)</param>
    public bool Launch(string? directory, string extraArgs = "")
    {
        try
        {
            var runDir = ResolveRunDirectory(directory);
            if (runDir is null)
            {
                return false;
            }

            // embedded python이 있으면 인자를 직접 붙여 실행합니다. (하드웨어 옵션 반영)
            var py = Path.Combine(runDir, "python_embeded", "python.exe");
            if (File.Exists(py))
            {
                var args = $"-s ComfyUI\\main.py --windows-standalone-build {extraArgs}".Trim();
                Process.Start(new ProcessStartInfo(py, args)
                {
                    UseShellExecute = true,
                    WorkingDirectory = runDir
                });
                return true;
            }

            // 폴백: 배치 파일 (CPU 옵션이면 run_cpu.bat 우선)
            var gpuBat = Path.Combine(runDir, "run_nvidia_gpu.bat");
            var cpuBat = Path.Combine(runDir, "run_cpu.bat");
            var useCpu = extraArgs.Contains("--cpu", StringComparison.OrdinalIgnoreCase);
            var bat = useCpu && File.Exists(cpuBat) ? cpuBat : File.Exists(gpuBat) ? gpuBat : cpuBat;
            if (!File.Exists(bat))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(bat)
            {
                UseShellExecute = true,
                WorkingDirectory = runDir
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 추천 이미지 모델(체크포인트)입니다. 다운로드 후 이 값들이 샘플링 설정에 적용됩니다.
    /// </summary>
    /// <param name="DisplayName">표시 이름입니다.</param>
    /// <param name="FileName">저장 파일명입니다.</param>
    /// <param name="Url">직접 다운로드 URL입니다. (HuggingFace 공개 · 인증 불필요)</param>
    /// <param name="Steps">권장 스텝입니다.</param>
    /// <param name="Cfg">권장 CFG입니다.</param>
    /// <param name="Sampler">권장 샘플러입니다.</param>
    /// <param name="Scheduler">권장 스케줄러입니다.</param>
    /// <param name="Width">권장 가로입니다.</param>
    /// <param name="Height">권장 세로입니다.</param>
    /// <param name="Note">용량·라이선스 안내입니다.</param>
    /// <param name="Uncensored">무검열(성인 표현 가능) 계열인지 여부입니다.</param>
    public sealed record ComfyModel(
        string DisplayName,
        string FileName,
        string Url,
        int Steps,
        double Cfg,
        string Sampler,
        string Scheduler,
        int Width,
        int Height,
        string Note,
        bool Uncensored = false);

    /// <summary>
    /// 8GB VRAM 기준 추천 모델 목록입니다. (뒤쪽 3종은 무검열 계열)
    /// </summary>
    public static IReadOnlyList<ComfyModel> RecommendedModels { get; } = new[]
    {
        new ComfyModel(
            "SDXL Base 1.0 (범용)",
            "sd_xl_base_1.0.safetensors",
            "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors?download=true",
            28, 6.5, "dpmpp_2m", "karras", 832, 1216,
            "약 6.9GB · OpenRAIL++(상업 조건부 허용) · 무난한 범용 모델"),
        new ComfyModel(
            "SDXL Turbo (초고속)",
            "sd_xl_turbo_1.0_fp16.safetensors",
            "https://huggingface.co/stabilityai/sdxl-turbo/resolve/main/sd_xl_turbo_1.0_fp16.safetensors?download=true",
            5, 1.2, "euler_ancestral", "normal", 768, 768,
            "약 6.9GB · 비상업(연구용) · 4~5스텝으로 매우 빠름, 저사양 적합"),
        new ComfyModel(
            "FLUX.1 schnell fp8 (최고 품질·상업 가능)",
            "flux1-schnell-fp8.safetensors",
            "https://huggingface.co/Comfy-Org/flux1-schnell/resolve/main/flux1-schnell-fp8.safetensors?download=true",
            4, 1.0, "euler", "simple", 1024, 1024,
            "약 17GB · Apache-2.0(상업 배포 가능) · 최고 품질, 8GB에선 다소 느림(자동 저사양)"),
        new ComfyModel(
            "🔞 RealVisXL V4.0 (실사·무검열)",
            "RealVisXL_V4.0.safetensors",
            "https://huggingface.co/SG161222/RealVisXL_V4.0/resolve/main/RealVisXL_V4.0.safetensors?download=true",
            30, 5.5, "dpmpp_2m", "karras", 832, 1216,
            "약 6.6GB · 무검열 실사 특화 · 성인 표현 가능(로컬 생성, 필터 없음)", true),
        new ComfyModel(
            "🔞 Illustrious-XL v0.1 (애니·무검열)",
            "Illustrious-XL-v0.1.safetensors",
            "https://huggingface.co/OnomaAIResearch/Illustrious-xl-early-release-v0/resolve/main/Illustrious-XL-v0.1.safetensors?download=true",
            28, 6.0, "euler_ancestral", "karras", 832, 1216,
            "약 6.6GB · 무검열 애니/일러스트 특화 · 성인 표현 가능", true),
        new ComfyModel(
            "🔞 NoobAI-XL v1.0 (애니·무검열)",
            "NoobAI-XL-v1.0.safetensors",
            "https://huggingface.co/Laxhar/noobai-XL-1.0/resolve/main/NoobAI-XL-v1.0.safetensors?download=true",
            28, 6.0, "euler_ancestral", "karras", 832, 1216,
            "약 6.9GB · 무검열 애니 특화(태그 프롬프트 강점) · 성인 표현 가능", true)
    };

    /// <summary>
    /// 지정한 추천 모델을 체크포인트 폴더로 내려받습니다.
    /// </summary>
    public async Task<bool> DownloadModelAsync(string? comfyDirectory, ComfyModel model, IProgress<string> progress)
    {
        var folder = GetCheckpointsFolder(comfyDirectory);
        if (folder is null)
        {
            progress.Report("먼저 ComfyUI를 설치하세요.");
            return false;
        }

        Directory.CreateDirectory(folder);
        var dest = Path.Combine(folder, model.FileName);
        if (File.Exists(dest) && new FileInfo(dest).Length > 100_000_000)
        {
            progress.Report($"{model.DisplayName}은(는) 이미 있습니다.");
            return true;
        }

        progress.Report($"{model.DisplayName} 다운로드를 시작합니다. ({model.Note})");
        var ok = await DownloadAsync(model.Url, dest, progress).ConfigureAwait(false);
        if (ok)
        {
            progress.Report($"모델 준비 완료: {model.FileName}");
        }
        else
        {
            try { File.Delete(dest); } catch { /* 무시 */ }
        }

        return ok;
    }

    /// <summary>
    /// 체크포인트(모델) 폴더 경로를 반환합니다. 없으면 null입니다.
    /// </summary>
    public string? GetCheckpointsFolder(string? directory)
    {
        var runDir = ResolveRunDirectory(directory);
        if (runDir is null)
        {
            return null;
        }

        var path = Path.Combine(runDir, "ComfyUI", "models", "checkpoints");
        return Directory.Exists(path) ? path : path; // 존재하지 않아도 경로는 알려줌
    }

    // 7-Zip 실행 파일을 확보합니다. 없으면 winget으로 설치합니다.
    private async Task<string?> EnsureSevenZipAsync(IProgress<string> progress)
    {
        var found = Find7Zip();
        if (found is not null)
        {
            return found;
        }

        if (!TryRun("winget", "--version"))
        {
            progress.Report("7-Zip이 없고 자동 설치기(winget)도 없습니다. https://www.7-zip.org 에서 설치해 주세요.");
            return null;
        }

        progress.Report("7-Zip을 설치하는 중입니다...");
        await RunAsync("winget", "install -e --id 7zip.7zip --accept-source-agreements --accept-package-agreements", null, progress).ConfigureAwait(false);

        found = Find7Zip();
        if (found is null)
        {
            progress.Report("7-Zip 설치를 확인하지 못했습니다. 프로그램을 다시 시작한 뒤 재시도해 주세요.");
        }

        return found;
    }

    private static string? Find7Zip()
    {
        var candidates = new[]
        {
            @"C:\Program Files\7-Zip\7z.exe",
            @"C:\Program Files (x86)\7-Zip\7z.exe"
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return TryRun("7z", "") ? "7z" : null;
    }

    // 파일을 스트리밍으로 내려받고 진행률(%)을 보고합니다.
    private static async Task<bool> DownloadAsync(string url, string destPath, IProgress<string> progress)
    {
        try
        {
            using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                progress.Report($"HTTP 오류: {(int)response.StatusCode}");
                return false;
            }

            var total = response.Content.Headers.ContentLength ?? -1L;
            await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var dest = File.Create(destPath);

            var buffer = new byte[1 << 20]; // 1MB
            long readTotal = 0;
            var lastPercent = -5;
            int read;
            while ((read = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                readTotal += read;

                if (total > 0)
                {
                    var percent = (int)(readTotal * 100 / total);
                    if (percent >= lastPercent + 5)
                    {
                        lastPercent = percent;
                        progress.Report($"다운로드 {percent}% ({readTotal / 1024 / 1024}MB / {total / 1024 / 1024}MB)");
                    }
                }
            }

            progress.Report("다운로드 완료.");
            return true;
        }
        catch (Exception ex)
        {
            progress.Report("다운로드 오류: " + ex.Message);
            return false;
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
