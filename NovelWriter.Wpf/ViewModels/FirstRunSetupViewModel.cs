using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelWriter.Wpf.Services;

namespace NovelWriter.Wpf.ViewModels;

/// <summary>
/// 첫 실행 설치 마법사 뷰모델입니다. Ollama(텍스트)와 ComfyUI(이미지)를 초보자도 버튼으로 설치·준비합니다.
/// </summary>
public partial class FirstRunSetupViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private readonly OllamaService _ollama;
    private readonly OllamaSetupService _ollamaSetup;
    private readonly ComfyUiSetupService _comfySetup;

    /// <summary>마법사를 닫아야 할 때 발생합니다.</summary>
    public event Action? CloseRequested;

    /// <summary>ComfyUI 설치 폴더 선택 콜백입니다.</summary>
    public Func<Task<string?>>? FolderResolver { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public FirstRunSetupViewModel(MainViewModel main, OllamaService ollama, OllamaSetupService ollamaSetup, ComfyUiSetupService comfySetup)
    {
        _main = main;
        _ollama = ollama;
        _ollamaSetup = ollamaSetup;
        _comfySetup = comfySetup;

        _selectedLlm = string.IsNullOrWhiteSpace(main.AiModel) ? "exaone3.5:7.8b" : main.AiModel;
        _selectedComfyModel = ComfyUiSetupService.RecommendedModels[0];
        _comfyInstallDir = string.IsNullOrWhiteSpace(main.ComfyUiPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NovelWriter")
            : main.ComfyUiPath;
    }

    /// <summary>추천 LLM 목록입니다.</summary>
    public IReadOnlyList<string> LlmModels { get; } = new[]
    {
        "exaone3.5:2.4b", "exaone3.5:7.8b", "qwen2.5:7b", "gemma2:9b",
        "fluffy/magnum-v4-9b", "dolphin3", "mannix/llama3.1-8b-abliterated"
    };

    /// <summary>ComfyUI 추천 이미지 모델 목록입니다.</summary>
    public IReadOnlyList<ComfyUiSetupService.ComfyModel> ComfyModels => ComfyUiSetupService.RecommendedModels;

    [ObservableProperty] private string _selectedLlm;
    [ObservableProperty] private ComfyUiSetupService.ComfyModel _selectedComfyModel;
    [ObservableProperty] private string _comfyInstallDir;
    [ObservableProperty] private string _log = string.Empty;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _ollamaStatus = "대기 중";
    [ObservableProperty] private string _comfyStatus = "대기 중";

    private void Report(string line) => Log += line + "\n";

    /// <summary>
    /// Ollama 설치 + 선택한 LLM 다운로드까지 한 번에 진행합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetupTextAiAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        var progress = new Progress<string>(Report);
        try
        {
            OllamaStatus = "Ollama 설치 중...";
            if (!await _ollamaSetup.InstallAsync(progress))
            {
                OllamaStatus = "❌ Ollama 설치 실패 (수동 설치 필요)";
                return;
            }

            // 서버가 뜰 시간을 잠깐 줍니다.
            _ollamaSetup.TryStartServer();
            for (var i = 0; i < 10 && !await _ollama.IsRunningAsync(); i++)
            {
                await Task.Delay(1500);
            }

            if (!await _ollama.IsRunningAsync())
            {
                OllamaStatus = "⚠ Ollama 설치됨. 서버 시작을 기다리는 중 — 잠시 후 다시 시도하세요.";
                return;
            }

            OllamaStatus = $"모델 다운로드 중: {SelectedLlm} ...";
            Report($"LLM 모델 '{SelectedLlm}' 다운로드를 시작합니다. (수 GB, 시간 소요)");
            var pullProgress = new Progress<OllamaPullProgress>(p =>
            {
                if (p.Percent >= 0)
                {
                    OllamaStatus = $"모델 다운로드 {Math.Round(p.Percent)}% ...";
                }
            });
            await _ollama.PullModelAsync(SelectedLlm, pullProgress);

            _main.AiModel = SelectedLlm;
            OllamaStatus = $"✅ 준비 완료: {SelectedLlm}";
            Report("텍스트 AI 준비 완료.");
        }
        catch (Exception ex)
        {
            OllamaStatus = "❌ 오류";
            Report("오류: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// ComfyUI(포터블) 설치 + 이미지 모델 다운로드까지 한 번에 진행합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetupImageAiAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        var progress = new Progress<string>(Report);
        try
        {
            ComfyStatus = "ComfyUI 설치 중... (약 1.5~2GB)";
            if (!await _comfySetup.InstallAsync(ComfyInstallDir, progress))
            {
                ComfyStatus = "❌ ComfyUI 설치 실패";
                return;
            }

            _main.ComfyUiPath = ComfyInstallDir;

            ComfyStatus = $"이미지 모델 다운로드 중: {SelectedComfyModel.DisplayName} ...";
            var ok = await _comfySetup.DownloadModelAsync(ComfyInstallDir, SelectedComfyModel, progress);
            ComfyStatus = ok ? $"✅ 준비 완료: {SelectedComfyModel.FileName}" : "⚠ ComfyUI 설치됨. 모델은 나중에 받을 수 있습니다.";
            Report("이미지 AI 준비 단계 완료.");
        }
        catch (Exception ex)
        {
            ComfyStatus = "❌ 오류";
            Report("오류: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 텍스트·이미지 AI를 순서대로 모두 설치합니다.
    /// </summary>
    [RelayCommand]
    private async Task SetupAllAsync()
    {
        await SetupTextAiAsync();
        await SetupImageAiAsync();
    }

    /// <summary>ComfyUI 설치 폴더를 선택합니다.</summary>
    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        if (FolderResolver is null)
        {
            return;
        }

        var folder = await FolderResolver();
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ComfyInstallDir = folder;
        }
    }

    /// <summary>설치를 마치고 마법사를 닫습니다.</summary>
    [RelayCommand]
    private async Task FinishAsync()
    {
        _main.SetupCompleted = true;
        await _main.SaveSettingsCommand.ExecuteAsync(null);
        CloseRequested?.Invoke();
    }

    /// <summary>나중에 설치하기로 하고 닫습니다. (다시 뜨지 않도록 완료 표시)</summary>
    [RelayCommand]
    private async Task SkipAsync()
    {
        _main.SetupCompleted = true;
        await _main.SaveSettingsCommand.ExecuteAsync(null);
        CloseRequested?.Invoke();
    }
}
