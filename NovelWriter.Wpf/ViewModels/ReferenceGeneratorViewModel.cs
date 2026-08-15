using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NovelWriter.Wpf.Services;

namespace NovelWriter.Wpf.ViewModels;

/// <summary>
/// 참고자료(.md) 생성기 뷰모델입니다. AI로 캐릭터/세계관 등 설정 문서를 만들어 저장합니다.
/// </summary>
public partial class ReferenceGeneratorViewModel : ObservableObject
{
    private readonly ChatService _chat;
    private readonly IImageBackend _imageBackend;
    private readonly string _referenceFolder;

    /// <summary>저장 경로 선택 콜백입니다. (제안 파일명, 하위 폴더 이름)</summary>
    public Func<string, string, Task<string?>>? SavePathResolver { get; set; }

    /// <summary>이미지 서버 설정 창을 여는 콜백입니다.</summary>
    public Action? OpenImageServerSettings { get; set; }

    /// <summary>이미지 서버를 실행하는 콜백입니다.</summary>
    public Action? LaunchImageServerCallback { get; set; }

    /// <summary>이미지 화풍(스타일) 접두입니다. (MainViewModel의 현재 스타일)</summary>
    public string StylePrefix { get; set; } = string.Empty;

    /// <summary>생성 전 이미지 모델 준비(없으면 자동 다운로드) 콜백입니다. (true=준비됨)</summary>
    public Func<Task<bool>>? EnsureImageModel { get; set; }

    /// <summary>생성 이미지를 메인 에디터 본문에 삽입하는 콜백입니다. (경로)</summary>
    public Action<string>? InsertImageToEditor { get; set; }

    /// <summary>생성 직전 화풍 설정 팝업을 띄우는 콜백입니다. (true=생성 진행)</summary>
    public Func<bool>? ConfirmStyleBeforeGenerate { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public ReferenceGeneratorViewModel(ChatService chat, IImageBackend imageBackend, string referenceFolder)
    {
        _chat = chat;
        _imageBackend = imageBackend;
        _referenceFolder = referenceFolder ?? string.Empty;
    }

    /// <summary>생성할 문서 유형 목록입니다.</summary>
    public IReadOnlyList<string> DocTypes { get; } = new[]
    {
        "캐릭터 설정", "세계관 설정", "시놉시스", "연표", "장소·배경 설정", "설정 용어집",
        "묘사·표현 모음", "감정·심리 묘사", "배경·풍경 묘사", "대사·문장 모음", "자유 형식"
    };

    [ObservableProperty]
    private string _docType = "캐릭터 설정";

    [ObservableProperty]
    private string _title = "새 참고자료";

    [ObservableProperty]
    private string _prompt = string.Empty;

    [ObservableProperty]
    private string _generatedContent = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>이미지 프롬프트입니다(영어, 비우면 내용에서 자동 생성).</summary>
    [ObservableProperty]
    private string _imagePrompt = string.Empty;

    /// <summary>생성된 이미지 파일 경로입니다(미리보기).</summary>
    [ObservableProperty]
    private string _generatedImagePath = string.Empty;

    /// <summary>이미지 서버 연결 상태 메시지입니다.</summary>
    [ObservableProperty]
    private string _imageServerStatus = "이미지 서버 상태: 미확인";

    /// <summary>
    /// AI로 참고자료 내용을 마크다운으로 생성합니다.
    /// </summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(Prompt))
        {
            StatusMessage = "요청 내용을 입력하세요.";
            return;
        }

        IsBusy = true;
        StatusMessage = "AI가 참고자료를 생성하는 중...";

        var system = BuildSystemPrompt();

        try
        {
            var result = await _chat.AskAsync(new[]
            {
                new ChatTurn("system", system),
                new ChatTurn("user", Prompt)
            });

            if (string.IsNullOrWhiteSpace(result))
            {
                StatusMessage = "생성 실패. AI 서버(Ollama)를 확인하세요.";
            }
            else
            {
                GeneratedContent = result;
                StatusMessage = "생성 완료. 내용을 확인·수정한 뒤 저장하세요.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "오류: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 저장 파일명의 베이스를 만듭니다. (넘버링은 View가 앞에 붙임)
    /// 캐릭터: 이름_나이_직업, 그 외: 제목.
    /// </summary>
    private string BuildFileNameBase()
    {
        if (DocType.Contains("캐릭터"))
        {
            var name = ExtractField("이름") ?? FirstHeading() ?? Title;
            var age = ExtractField("나이");
            var job = ExtractField("직업");

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) parts.Add(name.Trim());
            if (!string.IsNullOrWhiteSpace(age)) parts.Add(age.Trim());
            if (!string.IsNullOrWhiteSpace(job)) parts.Add(job.Trim());

            if (parts.Count > 0)
            {
                return string.Join("_", parts);
            }
        }

        return string.IsNullOrWhiteSpace(Title) ? "reference" : Title;
    }

    /// <summary>생성 결과의 첫 제목(# ...)을 반환합니다.</summary>
    private string? FirstHeading()
    {
        var m = Regex.Match(GeneratedContent ?? string.Empty, @"^\s*#+\s*(.+)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    /// <summary>생성 결과에서 "라벨: 값" 또는 "- **라벨**: 값" 형태의 값을 추출합니다.</summary>
    private string? ExtractField(string label)
    {
        var pattern = $@"[-*\s]*\**\s*{Regex.Escape(label)}\s*\**\s*[:：]\s*(.+)";
        var m = Regex.Match(GeneratedContent ?? string.Empty, pattern);
        if (!m.Success)
        {
            return null;
        }

        // 값에서 마크다운 강조·괄호 등 정리
        var value = m.Groups[1].Value.Trim().TrimEnd('*', ' ', '.', ',');
        // 나이는 숫자만 남기기 (예: "35세" → "35세" 유지, "약 30대" → 그대로)
        return value.Length > 20 ? value[..20].Trim() : value;
    }

    /// <summary>
    /// 유형에 맞는 하위 폴더 이름을 반환합니다. (폴더로 자동 분류)
    /// </summary>
    private static string SubFolderFor(string docType)
    {
        if (docType.Contains("캐릭터")) return "Characters";
        if (docType.Contains("세계관")) return "World";
        if (docType.Contains("장소") || docType.Contains("배경")) return "Backgrounds";
        if (docType.Contains("시놉시스")) return "Synopsis";
        if (docType.Contains("묘사") || docType.Contains("표현") || docType.Contains("문장") || docType.Contains("대사")) return "Descriptions";
        return string.Empty;
    }

    /// <summary>
    /// 유형에 맞는 시스템 프롬프트를 만듭니다. (묘사·표현 계열은 소설 문장 특화)
    /// </summary>
    private string BuildSystemPrompt()
    {
        if (DocType.Contains("묘사") || DocType.Contains("표현") || DocType.Contains("문장") || DocType.Contains("대사"))
        {
            return "당신은 문장력이 뛰어난 소설가입니다. 요청한 주제·상황·분위기에 어울리는 '멋진 묘사와 표현'을 여러 개 만들어 주세요. "
                + "바로 소설 본문에 옮겨 쓸 수 있는 완성된 문장 형태로, 진부한 표현을 피하고 참신하고 감각적으로 작성하세요. "
                + "필요하면 상황별 소제목(##)으로 분류하고, 각 표현을 목록(-)으로 정리하세요. "
                + "반드시 한국어로 작성하고, 설명·머리말 없이 마크다운 본문만 출력하세요.";
        }

        return $"당신은 소설 설정 작가입니다. 요청에 맞는 '{DocType}'을(를) 마크다운(.md) 형식으로 작성하세요. "
            + "제목(#), 소제목(##), 목록(-), 굵게(**) 를 적절히 활용하고, 반드시 한국어로 작성하세요. "
            + "설명·머리말 없이 마크다운 문서 본문만 출력하세요.";
    }

    // ── 이미지 생성 ──

    /// <summary>이미지 서버 연결 상태를 확인합니다.</summary>
    [RelayCommand]
    private async Task CheckImageServerAsync()
    {
        ImageServerStatus = "이미지 서버 상태: 확인 중...";
        var running = await _imageBackend.IsRunningAsync();
        ImageServerStatus = running
            ? "이미지 서버 상태: ✅ 연결됨"
            : "이미지 서버 상태: ❌ 응답 없음 → [서버 실행] 또는 [서버 설정]";
    }

    /// <summary>이미지 서버 설정 창을 엽니다.</summary>
    [RelayCommand]
    private void OpenImageServer() => OpenImageServerSettings?.Invoke();

    /// <summary>생성된 이미지를 본문에 삽입합니다.</summary>
    [RelayCommand]
    private void InsertImage()
    {
        if (!string.IsNullOrWhiteSpace(GeneratedImagePath))
        {
            InsertImageToEditor?.Invoke(GeneratedImagePath);
        }
    }

    /// <summary>이미지 서버를 실행하고 잠시 후 연결을 확인합니다.</summary>
    [RelayCommand]
    private async Task LaunchImageServerAsync()
    {
        if (LaunchImageServerCallback is null)
        {
            OpenImageServerSettings?.Invoke();
            return;
        }

        ImageServerStatus = "이미지 서버 상태: 서버를 실행하는 중... (준비까지 수십 초~수 분)";
        LaunchImageServerCallback.Invoke();
        await Task.Delay(8000);
        await CheckImageServerAsync();
    }

    /// <summary>
    /// 생성된 참고자료 내용을 바탕으로 이미지를 생성합니다. (캐릭터/배경 삽화)
    /// </summary>
    [RelayCommand]
    private async Task GenerateImageAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ImagePrompt) && string.IsNullOrWhiteSpace(GeneratedContent))
        {
            StatusMessage = "먼저 참고자료를 생성하거나 이미지 프롬프트를 입력하세요.";
            return;
        }

        // 생성 전 화풍 설정 팝업 (취소 시 생성 안 함)
        if (ConfirmStyleBeforeGenerate is not null && !ConfirmStyleBeforeGenerate())
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "이미지를 생성하는 중...";

        try
        {
            // 프롬프트가 비었으면 생성된 내용에서 영어 SD 프롬프트를 자동 생성
            if (string.IsNullOrWhiteSpace(ImagePrompt))
            {
                var built = await BuildImagePromptAsync();
                if (!string.IsNullOrWhiteSpace(built))
                {
                    ImagePrompt = built.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(ImagePrompt))
            {
                StatusMessage = "이미지 프롬프트 생성 실패. (AI 서버 확인)";
                return;
            }

            if (EnsureImageModel is not null && !await EnsureImageModel())
            {
                StatusMessage = "이미지 모델이 준비되지 않았습니다. [서버 실행]/[서버 설정]에서 모델을 준비하세요.";
                return;
            }

            var stylePrefix = string.IsNullOrWhiteSpace(StylePrefix) ? "storybook illustration, detailed" : StylePrefix;
            var fullPrompt = $"{stylePrefix}, {CompositionHintFor(DocType)}{ImagePrompt}";
            var result = await _imageBackend.GenerateAsync(fullPrompt);
            if (result is null)
            {
                StatusMessage = "이미지 생성 실패 — 이미지 서버가 실행 중이 아니거나 모델이 없습니다. [서버 실행]/[서버 설정] 확인.";
                ImageServerStatus = "이미지 서버 상태: ❌ 생성 실패 (실행/모델 확인)";
                return;
            }

            GeneratedImagePath = SaveImage(ImageSubFolderFor(DocType), BuildFileNameBase(), result.ImageBytes);
            StatusMessage = $"이미지 저장 완료: {Path.GetFileName(GeneratedImagePath)}";
        }
        catch (Exception ex)
        {
            StatusMessage = "오류: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // 생성된 내용을 영어 SD 프롬프트로 변환합니다.
    private async Task<string?> BuildImagePromptAsync()
    {
        var system = "You convert Korean novel-setting text into a concise English Stable Diffusion prompt. "
            + "Output ONLY comma-separated English keywords/short phrases describing visual appearance, clothing, mood, and scene. "
            + "No full sentences, no Korean, no explanations.";
        var user = $"유형: {DocType}\n내용:\n{GeneratedContent}";
        return await _chat.AskAsync(new[]
        {
            new ChatTurn("system", system),
            new ChatTurn("user", user)
        });
    }

    // 유형별 구도 힌트를 반환합니다. (화풍은 StylePrefix가 담당)
    private static string CompositionHintFor(string docType)
    {
        if (docType.Contains("캐릭터"))
        {
            return "character reference sheet, full body, ";
        }

        if (docType.Contains("장소") || docType.Contains("배경") || docType.Contains("풍경"))
        {
            return "scenery, background art, wide shot, ";
        }

        return string.Empty;
    }

    // 유형별 이미지 저장 하위 폴더를 반환합니다.
    private static string ImageSubFolderFor(string docType)
    {
        if (docType.Contains("캐릭터")) return "Characters/Sheets";
        if (docType.Contains("장소") || docType.Contains("배경") || docType.Contains("풍경")) return "Backgrounds";
        return "Illustrations";
    }

    // 이미지를 참고자료 폴더 하위에 PNG로 저장합니다.
    private string SaveImage(string subDirectory, string name, byte[] bytes)
    {
        var baseDir = !string.IsNullOrWhiteSpace(_referenceFolder) && Directory.Exists(_referenceFolder)
            ? _referenceFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovelWriter");

        var directory = Path.Combine(baseDir, subDirectory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(directory);

        var file = Path.Combine(directory, MakeSafeFileName(name) + ".png");
        File.WriteAllBytes(file, bytes);
        return file;
    }

    private static string MakeSafeFileName(string name)
    {
        var safe = string.IsNullOrWhiteSpace(name) ? "image" : name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(c, '_');
        }

        return safe.Length > 60 ? safe[..60] : safe;
    }

    /// <summary>
    /// 생성된 내용을 .md 파일로 저장합니다.
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SavePathResolver is null || string.IsNullOrWhiteSpace(GeneratedContent))
        {
            StatusMessage = "저장할 내용이 없습니다.";
            return;
        }

        var path = await SavePathResolver(BuildFileNameBase(), SubFolderFor(DocType));
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, GeneratedContent);
        StatusMessage = $"저장했습니다: {Path.GetFileName(path)}";
    }
}
