using System.IO;
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

    /// <summary>저장 경로 선택 콜백입니다.</summary>
    public Func<string, Task<string?>>? SavePathResolver { get; set; }

    /// <summary>
    /// 뷰모델을 초기화합니다.
    /// </summary>
    public ReferenceGeneratorViewModel(ChatService chat)
    {
        _chat = chat;
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

        var suggested = string.IsNullOrWhiteSpace(Title) ? "reference" : Title;
        var path = await SavePathResolver(suggested);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        await File.WriteAllTextAsync(path, GeneratedContent);
        StatusMessage = $"저장했습니다: {Path.GetFileName(path)}";
    }
}
