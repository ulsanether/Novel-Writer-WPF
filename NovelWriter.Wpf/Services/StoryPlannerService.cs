using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NovelWriter.Wpf.Models;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 원고에서 추출한 작품 설정과 등장인물입니다.
/// </summary>
public sealed record ExtractedSettings(
    string Genre,
    string Era,
    string World,
    string CoreEvent,
    string Ending,
    IReadOnlyList<NovelWriter.Wpf.Models.StoryCharacter> Characters);

/// <summary>
/// 시놉시스 → 장 → Scene → 본문을 각각 별도 AI 작업으로 생성합니다. (로컬 모델용 컨텍스트 최소화)
/// </summary>
public sealed class StoryPlannerService
{
    private readonly ChatService _chat;

    /// <summary>
    /// 서비스를 초기화합니다.
    /// </summary>
    public StoryPlannerService(ChatService chat)
    {
        _chat = chat;
    }

    /// <summary>현재 AI 모델이 무검열 계열인지 여부입니다. (수위 옵션 활성화 기준)</summary>
    public bool IsUncensoredModel => NovelWriter.Wpf.ModelBadgeKeywords.Matches(_chat.Model, NovelWriter.Wpf.ModelBadgeKeywords.Uncensored);

    /// <summary>
    /// 본문 생성에 적용할 수위(성인) 지시문을 만듭니다. 무검열 모델이 아니거나 모두 '없음'이면 빈 문자열.
    /// </summary>
    public string BuildMaturityInstruction(StoryProject p)
    {
        if (!IsUncensoredModel)
        {
            return string.Empty; // 검열 모델에는 강한 성인 지시를 넣지 않음(거부·왜곡 방지)
        }

        var sexual = SexualInstruction(p.SexualLevel, p.ContentRating);
        var violence = ViolenceInstruction(p.ViolenceLevel);
        if (string.IsNullOrEmpty(sexual) && string.IsNullOrEmpty(violence) && p.ContentRating != "18+")
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("[표현 수위 지시]\n");
        if (p.ContentRating == "18+")
        {
            sb.Append("이 작품은 성인(18세 이상) 전용입니다. 표현 수위 제한이 없으며, 검열·완곡·생략 없이 요청된 강도로 작성하세요.\n");
        }
        else
        {
            sb.Append($"콘텐츠 등급: {p.ContentRating}. 해당 등급에 맞는 수위로 작성하세요.\n");
        }

        if (!string.IsNullOrEmpty(sexual))
        {
            sb.Append(sexual).Append('\n');
        }

        if (!string.IsNullOrEmpty(violence))
        {
            sb.Append(violence).Append('\n');
        }

        return sb.ToString();
    }

    private static string SexualInstruction(string level, string rating) => level switch
    {
        "약함" => "선정성: 은근한 로맨스·긴장감 정도만 표현하세요.",
        "중간" => "선정성: 선정적인 분위기와 암시적 묘사까지 포함하세요.",
        "강함" => "선정성: 노골적인 성적 묘사를 포함해 과감하게 작성하세요.",
        "노골적" => rating == "18+"
            ? "선정성(최대): 매우 노골적이고 자극적인 성인 성애 장면을 구체적·상세하게, 수위를 낮추지 말고 과감하게 서술하세요."
            : "선정성: 강한 성적 묘사를 포함하세요.",
        _ => string.Empty
    };

    private static string ViolenceInstruction(string level) => level switch
    {
        "약함" => "폭력성: 가벼운 갈등·긴장 수준으로만 표현하세요.",
        "중간" => "폭력성: 폭력 장면과 부상 묘사를 포함하세요.",
        "강함" => "폭력성: 격렬한 폭력과 유혈 묘사를 포함하세요.",
        "잔혹" => "폭력성(최대): 잔혹하고 고어한 묘사까지 구체적으로 서술하세요.",
        _ => string.Empty
    };

    /// <summary>
    /// 전체 시놉시스를 생성합니다. (본문은 쓰지 않음)
    /// </summary>
    public async Task<string?> GenerateSynopsisAsync(StoryProject project)
    {
        const string system =
            "당신은 소설 기획 전문가입니다. 주어진 작품 설정으로 '전체 시놉시스'만 작성하세요. "
            + "장면 묘사나 대사 같은 본문은 쓰지 말고, 큰 사건의 흐름만 8~15문장으로 요약하세요. 한국어로 작성합니다.";
        var user = BuildBible(project) + "\n\n[요청] 위 설정에 맞는 전체 시놉시스를 작성하세요.";
        return await EnsureKoreanAsync(await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) }));
    }

    /// <summary>이야기 단계 순서입니다. (발단·전개·위기·절정·결말)</summary>
    private static readonly string[] PhaseOrder = { "발단", "전개", "위기", "절정", "결말" };

    /// <summary>
    /// 장 위치(0-based)와 전체 장 수로 이야기 단계를 계산합니다. (발단→전개→위기→절정→결말)
    /// </summary>
    public static string PhaseForIndex(int index, int total)
    {
        if (total <= 1)
        {
            return "발단";
        }

        if (index <= 0)
        {
            return "발단";
        }

        if (index >= total - 1)
        {
            return "결말";
        }

        var ratio = (double)index / (total - 1);
        if (ratio < 0.45)
        {
            return "전개";
        }

        return ratio < 0.75 ? "위기" : "절정";
    }

    /// <summary>단계별 서술 지침입니다.</summary>
    public static string PhaseGuidance(string phase) => phase switch
    {
        "발단" => "이야기의 도입부. 배경·인물·일상을 소개하고 중심 갈등의 씨앗을 심는다. 아직 큰 사건은 터지지 않으며, 결말을 미리 드러내지 않는다.",
        "전개" => "사건이 본격화되는 상승 구간. 갈등을 키우고 인물의 목표와 관계를 구체화한다.",
        "위기" => "갈등이 심화되고 위기가 고조된다. 긴장을 높이고 되돌릴 수 없는 선택으로 몰아간다.",
        "절정" => "이야기의 정점(클라이맥스). 핵심 갈등이 폭발하고 가장 큰 사건이 벌어진다.",
        "결말" => "갈등을 해소하고 이야기를 마무리한다. 설정한 결말 방향에 맞춰 여운을 남긴다.",
        _ => string.Empty
    };

    /// <summary>
    /// 시놉시스를 바탕으로 장 구성을 생성합니다. (발단·전개·위기·절정·결말 구조 반영)
    /// </summary>
    public async Task<List<ChapterNode>> GenerateChaptersAsync(StoryProject project)
    {
        var total = Math.Max(1, project.ChapterCount);
        // 각 장에 배치할 이야기 단계 계획
        var plan = string.Join("\n", Enumerable.Range(0, total).Select(i => $"{i + 1}장 → {PhaseForIndex(i, total)}"));

        const string system = "당신은 소설 구조 설계자입니다. 요청한 JSON 배열만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 JSON 문자열 값은 반드시 한국어로 작성하고 영어를 쓰지 마세요.";
        var user = BuildBible(project)
            + $"\n\n[전체 시놉시스]\n{project.Synopsis}\n\n"
            + $"[요청] 이 작품을 정확히 {total}개의 장으로 나누세요. "
            + "전체를 **발단 → 전개 → 위기 → 절정 → 결말**의 이야기 흐름으로 구성하고, 아래 단계 배치를 '정확히' 따르세요. "
            + "발단 장에서는 결말이나 큰 사건을 미리 터뜨리지 말고 도입에 집중하고, 절정 장에서 핵심 갈등이 폭발하며, 결말 장에서 마무리하세요.\n"
            + $"[장별 이야기 단계]\n{plan}\n\n"
            + "각 장을 JSON 배열의 원소로 '순서대로' 출력하세요. 각 원소는 다음 키를 가집니다: "
            + "\"phase\"(이야기 단계: 발단/전개/위기/절정/결말 중 하나), \"title\"(장 제목), \"summary\"(장 요약), \"purpose\"(장 목적), \"conflict\"(갈등), \"reveal\"(반전), \"ending\"(종료 상태). "
            + "JSON 배열만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var chapters = new List<ChapterNode>();

        foreach (var element in EnumerateJsonArray(reply))
        {
            chapters.Add(new ChapterNode
            {
                Title = await EnsureKoreanAsync(GetString(element, "title")),
                Summary = await EnsureKoreanAsync(GetString(element, "summary")),
                Purpose = await EnsureKoreanAsync(GetString(element, "purpose")),
                Conflict = await EnsureKoreanAsync(GetString(element, "conflict")),
                Reveal = await EnsureKoreanAsync(GetString(element, "reveal")),
                Ending = await EnsureKoreanAsync(GetString(element, "ending"))
            });
        }

        // 단계는 위치 기준으로 확정합니다. (모델이 잘못 넣어도 순서가 곧 구조)
        for (var i = 0; i < chapters.Count; i++)
        {
            chapters[i].Phase = PhaseForIndex(i, chapters.Count);
        }

        return chapters;
    }

    /// <summary>
    /// 한 장을 Scene 단위로 분할합니다.
    /// </summary>
    public async Task<List<SceneNode>> GenerateScenesAsync(StoryProject project, ChapterNode chapter)
    {
        const string system = "당신은 소설 구조 설계자입니다. 요청한 JSON 배열만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 JSON 문자열 값은 반드시 한국어로 작성하고 영어를 쓰지 마세요.";
        var phaseLine = string.IsNullOrWhiteSpace(chapter.Phase)
            ? string.Empty
            : $"[이 장의 이야기 단계] {chapter.Phase} — {PhaseGuidance(chapter.Phase)}\n이 단계에 맞게 Scene의 긴장도와 사건 규모를 조절하세요.\n\n";
        var user = BuildBible(project)
            + $"\n\n[전체 시놉시스 요약]\n{Shorten(project.Synopsis, 600)}\n\n"
            + phaseLine
            + $"[현재 장]\n제목: {chapter.Title}\n요약: {chapter.Summary}\n목적: {chapter.Purpose}\n갈등: {chapter.Conflict}\n반전: {chapter.Reveal}\n종료: {chapter.Ending}\n\n"
            + "[요청] 이 장을 3~6개의 Scene으로 나누세요. 각 Scene을 JSON 배열의 원소로 출력하세요. 각 원소 키: "
            + "\"title\"(Scene 제목), \"summary\"(요약), \"goal\"(목표), \"characters\"(등장인물), \"location\"(장소), \"conflict\"(갈등), \"result\"(결과), \"nextLink\"(다음 Scene 연결). "
            + "JSON 배열만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var scenes = new List<SceneNode>();

        foreach (var element in EnumerateJsonArray(reply))
        {
            scenes.Add(new SceneNode
            {
                Title = await EnsureKoreanAsync(GetString(element, "title")),
                Summary = await EnsureKoreanAsync(GetString(element, "summary")),
                Goal = await EnsureKoreanAsync(GetString(element, "goal")),
                Characters = await EnsureKoreanAsync(GetString(element, "characters")),
                Location = await EnsureKoreanAsync(GetString(element, "location")),
                Conflict = await EnsureKoreanAsync(GetString(element, "conflict")),
                Result = await EnsureKoreanAsync(GetString(element, "result")),
                NextLink = await EnsureKoreanAsync(GetString(element, "nextLink"))
            });
        }

        return scenes;
    }

    /// <summary>
    /// 한 Scene의 실제 본문을 작성합니다.
    /// </summary>
    /// <param name="dialogueRatio">대사 비율(0~100). 0=묘사만, 100=대사만.</param>
    public async Task<string?> GenerateSceneContentAsync(
        StoryProject project, ChapterNode chapter, SceneNode scene, string previousSceneSummary, int dialogueRatio = 50)
    {
        const string system =
            "당신은 소설가입니다. 주어진 설정과 Scene 조건에 맞는 '소설 본문'을 한국어로 작성하세요. "
            + "설정을 벗어나거나 금지사항을 위반하지 마세요. "
            + "매우 중요: '제목/요약/목표/등장인물/장소/갈등/결과' 같은 설정 항목이나 목록·머리말·라벨을 절대 출력하지 말고, "
            + "오직 장면의 서술과 인물의 대사로 이루어진 소설 본문만 작성하세요.";
        var phaseLine = string.IsNullOrWhiteSpace(chapter.Phase)
            ? string.Empty
            : $"[이야기 단계] {chapter.Phase} — {PhaseGuidance(chapter.Phase)}\n\n";
        var user = BuildBible(project)
            + $"\n\n[전체 시놉시스 요약]\n{Shorten(project.Synopsis, 500)}\n\n"
            + phaseLine
            + $"[현재 장]\n{chapter.Title} — {chapter.Summary}\n\n"
            + (string.IsNullOrWhiteSpace(previousSceneSummary) ? string.Empty : $"[이전 Scene]\n{previousSceneSummary}\n\n")
            + $"[작성할 Scene 조건 — 이 항목들은 참고만 하고 본문에 옮겨 적지 마세요]\n제목: {scene.Title}\n목표: {scene.Goal}\n등장인물: {scene.Characters}\n장소: {scene.Location}\n갈등: {scene.Conflict}\n결과: {scene.Result}\n\n"
            + $"[문체 지시]\n{DialogueStyleInstruction(dialogueRatio)}\n\n"
            + MaturitySection(project)
            + "[요청] 위 조건을 반영하되, 항목을 나열하지 말고 소설 본문만 작성하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        return await EnsureKoreanAsync(StripSceneMeta(reply));
    }

    // 수위 지시가 있으면 프롬프트 섹션으로 감쌉니다.
    private string MaturitySection(StoryProject project)
    {
        var maturity = BuildMaturityInstruction(project);
        return string.IsNullOrEmpty(maturity) ? string.Empty : maturity + "\n";
    }

    /// <summary>
    /// 대사/묘사 비율에 따른 문체 지시 문구를 만듭니다.
    /// </summary>
    private static string DialogueStyleInstruction(int dialogueRatio)
    {
        var ratio = Math.Clamp(dialogueRatio, 0, 100);
        if (ratio >= 95)
        {
            return "이 본문은 거의 전부 인물의 '대사'로만 구성하세요. 서술·묘사는 최소화하고 대화(따옴표) 위주로 작성합니다.";
        }

        if (ratio <= 5)
        {
            return "이 본문은 대사 없이 '묘사와 서술'로만 구성하세요. 인물의 대사(따옴표 대화)를 넣지 마세요.";
        }

        return $"대사와 묘사의 비율을 대략 대사 {ratio}% / 묘사 {100 - ratio}%로 맞춰 작성하세요.";
    }

    /// <summary>
    /// 한 Scene을 세부 비트로 나눈 뒤 각 비트를 여러 문단으로 작성해 이어붙입니다. (긴 본문)
    /// </summary>
    public async Task<string?> GenerateSceneContentDetailedAsync(
        StoryProject project, ChapterNode chapter, SceneNode scene, string previousSceneSummary,
        int dialogueRatio = 50, IProgress<string>? progress = null)
    {
        var beats = await GenerateSceneBeatsAsync(project, chapter, scene);
        if (beats.Count == 0)
        {
            // 비트 분할 실패 시 기존 단일 생성으로 대체합니다.
            return await GenerateSceneContentAsync(project, chapter, scene, previousSceneSummary, dialogueRatio);
        }

        var builder = new StringBuilder();
        var previousProse = previousSceneSummary;

        for (var i = 0; i < beats.Count; i++)
        {
            progress?.Report($"본문 작성 {i + 1}/{beats.Count} 비트...");

            const string system =
                "당신은 소설가입니다. 주어진 '비트(장면 조각)'를 서너 문단 이상의 생생한 소설 본문으로 작성하세요. "
                + "인물의 대사·행동·심리·배경 묘사를 충분히 넣어 길고 몰입감 있게 쓰세요. "
                + "매우 중요: '제목/목표/등장인물/장소/갈등/결과' 같은 설정 항목이나 라벨·목록을 절대 출력하지 말고, 오직 소설 본문만 쓰세요. "
                + "설정과 금지사항을 지키고, 자연스러운 소설 문장으로만 작성합니다. 한국어로 작성합니다.";
            var user = BuildBible(project)
                + $"\n\n[현재 장]\n{chapter.Title} — {chapter.Summary}\n"
                + $"[현재 Scene]\n{scene.Title} / 목표: {scene.Goal} / 장소: {scene.Location} / 등장인물: {scene.Characters}\n\n"
                + (string.IsNullOrWhiteSpace(previousProse) ? string.Empty : $"[직전 내용 요약]\n{Shorten(previousProse, 400)}\n\n")
                + $"[문체 지시]\n{DialogueStyleInstruction(dialogueRatio)}\n\n"
                + MaturitySection(project)
                + $"[이번에 쓸 비트]\n{beats[i]}\n\n[요청] 이 비트를 이어지는 소설 본문으로 길게 작성하세요.";

            var prose = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
            var cleaned = StripSceneMeta(prose);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                var korean = await EnsureKoreanAsync(cleaned);
                builder.Append(korean.Trim()).Append("\n\n");
                previousProse = korean;
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Scene을 세부 사건(비트) 목록으로 분해합니다.
    /// </summary>
    private async Task<List<string>> GenerateSceneBeatsAsync(StoryProject project, ChapterNode chapter, SceneNode scene)
    {
        const string system = "당신은 소설 구조 설계자입니다. 문자열 JSON 배열만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 값은 반드시 한국어로 작성하세요.";
        var phaseLine = string.IsNullOrWhiteSpace(chapter.Phase)
            ? string.Empty
            : $"[이야기 단계] {chapter.Phase} — {PhaseGuidance(chapter.Phase)}\n";
        var user = BuildBible(project)
            + $"\n\n[현재 장]\n{chapter.Title} — {chapter.Summary}\n"
            + phaseLine
            + $"[현재 Scene]\n제목: {scene.Title}\n요약: {scene.Summary}\n목표: {scene.Goal}\n갈등: {scene.Conflict}\n결과: {scene.Result}\n\n"
            + "[요청] 이 Scene을 시간 순서대로 4~6개의 세부 사건(비트)으로 나누세요. 각 비트를 한 문장으로 설명한 "
            + "JSON 문자열 배열(예: [\"...\", \"...\"])로만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var beats = new List<string>();
        foreach (var element in EnumerateJsonArray(reply))
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    beats.Add(text);
                }
            }
        }

        return beats;
    }

    /// <summary>
    /// 작품 전체의 스토리 일관성을 검사하고 경고 목록을 반환합니다.
    /// </summary>
    public async Task<string?> CheckConsistencyAsync(StoryProject project)
    {
        const string system =
            "당신은 소설 편집자입니다. 스토리의 일관성 오류(죽은 인물 재등장, 정보 노출 시점 모순, 이동 경로 누락, 설정 위반 등)를 찾아 "
            + "한국어로 간결한 목록(각 줄 '⚠ ...')으로 알려주세요. 문제가 없으면 '발견된 문제가 없습니다.'라고만 답하세요.";

        var builder = new StringBuilder();
        builder.AppendLine(BuildBible(project));
        builder.AppendLine($"\n[전체 시놉시스]\n{project.Synopsis}\n");
        builder.AppendLine("[장 구성]");
        var chapterIndex = 1;
        foreach (var chapter in project.Chapters)
        {
            builder.AppendLine($"{chapterIndex}장 {chapter.Title}: {chapter.Summary} (반전: {chapter.Reveal})");
            var sceneIndex = 1;
            foreach (var scene in chapter.Scenes)
            {
                builder.AppendLine($"  - Scene {chapterIndex}-{sceneIndex}: {scene.Summary}");
                sceneIndex++;
            }

            chapterIndex++;
        }

        builder.Append("\n[요청] 위 구성의 일관성 문제를 찾아주세요.");

        return await EnsureKoreanAsync(await _chat.AskAsync(new[]
        {
            new ChatTurn("system", system),
            new ChatTurn("user", builder.ToString())
        }));
    }

    // ────────────────────────── 이미지 프롬프트 생성 (영어 · SD용) ──────────────────────────

    /// <summary>
    /// 캐릭터 정보에서 이미지 생성용 영어 외형 프롬프트를 만듭니다. (캐릭터 시트/레퍼런스용)
    /// </summary>
    public async Task<string?> GenerateCharacterImagePromptAsync(StoryProject project, StoryCharacter character)
    {
        const string system =
            "You are a prompt engineer for Stable Diffusion. Output ONLY a concise English image prompt as comma-separated tags "
            + "(age, gender, hair color/style, eye color, body build, clothing, distinctive features). "
            + "No sentences, no explanation, no quotes.";
        var user =
            $"Character:\nname: {character.Name}\npersonality: {character.Personality}\ngoal: {character.Goal}\nrelationships: {character.Relationships}\n"
            + $"Genre: {project.Genre}, Era: {project.Era}, World: {project.World}\n"
            + "Create an English appearance prompt for a full-body character reference (turnaround/character sheet).";

        return await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
    }

    /// <summary>
    /// 씬 정보 + 등장인물 외형 + 화풍을 결합한 영어 삽화 프롬프트를 만듭니다.
    /// </summary>
    public async Task<string?> GenerateSceneImagePromptAsync(StoryProject project, ChapterNode chapter, SceneNode scene)
    {
        // 씬에 등장하는 인물의 외형 프롬프트(AppearancePrompt)를 모읍니다. (캐릭터 연동 · 일관성)
        var appearances = new StringBuilder();
        foreach (var c in project.Characters)
        {
            if (!string.IsNullOrWhiteSpace(c.Name)
                && !string.IsNullOrWhiteSpace(c.AppearancePrompt)
                && scene.Characters.Contains(c.Name, StringComparison.Ordinal))
            {
                appearances.AppendLine($"{c.Name}: {c.AppearancePrompt}");
            }
        }

        const string system =
            "You are a prompt engineer for Stable Diffusion. Output ONLY a concise English image prompt as comma-separated tags. "
            + "No sentences, no explanation, no quotes.";
        var user =
            $"Scene:\nlocation: {scene.Location}\ncharacters: {scene.Characters}\nsituation: {scene.Summary}\nmood/conflict: {scene.Conflict}\n\n"
            + (appearances.Length > 0 ? $"Character appearances (keep consistent):\n{appearances}\n" : string.Empty)
            + $"Genre: {project.Genre}, Era: {project.Era}, World: {project.World}\n"
            + "Create an English illustration prompt for this scene, keeping the given character appearances consistent.";

        return await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
    }

    // ────────────────────────── 원고 역분석 (원고 → 설계) ──────────────────────────

    /// <summary>
    /// 원고를 분석해 작품 설정과 등장인물을 추출합니다.
    /// </summary>
    public async Task<ExtractedSettings?> ExtractSettingsAsync(string manuscript)
    {
        const string system = "당신은 소설 분석가입니다. 요청한 JSON 객체만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 JSON 문자열 값(장르·시대·세계관·인물 정보 등)은 반드시 한국어로 작성하고 영어를 쓰지 마세요.";
        var user = "[소설 원고]\n" + Shorten(manuscript, 12000)
            + "\n\n[요청] 위 원고를 분석해 작품 설정을 JSON 객체로 출력하세요. 키: "
            + "\"genre\"(장르), \"era\"(시대), \"world\"(세계관), \"coreEvent\"(핵심 사건), \"ending\"(결말 방향), "
            + "\"characters\"(배열, 각 원소 \"name\",\"personality\",\"goal\",\"secret\",\"relationships\"). JSON 객체만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var root = ParseJsonObject(reply);
        if (root is null)
        {
            return null;
        }

        var characters = new List<StoryCharacter>();
        if (root.Value.TryGetProperty("characters", out var chars) && chars.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in chars.EnumerateArray())
            {
                characters.Add(new StoryCharacter
                {
                    Name = await EnsureKoreanAsync(GetString(c, "name")),
                    Personality = await EnsureKoreanAsync(GetString(c, "personality")),
                    Goal = await EnsureKoreanAsync(GetString(c, "goal")),
                    Secret = await EnsureKoreanAsync(GetString(c, "secret")),
                    Relationships = await EnsureKoreanAsync(GetString(c, "relationships"))
                });
            }
        }

        return new ExtractedSettings(
            await EnsureKoreanAsync(GetString(root.Value, "genre")),
            await EnsureKoreanAsync(GetString(root.Value, "era")),
            await EnsureKoreanAsync(GetString(root.Value, "world")),
            await EnsureKoreanAsync(GetString(root.Value, "coreEvent")),
            await EnsureKoreanAsync(GetString(root.Value, "ending")),
            characters);
    }

    /// <summary>
    /// 원고에서 전체 시놉시스를 추출합니다.
    /// </summary>
    public async Task<string?> ExtractSynopsisAsync(string manuscript)
    {
        const string system =
            "당신은 소설 분석가입니다. 주어진 원고의 전체 시놉시스를 8~15문장으로 요약하세요. "
            + "큰 사건 흐름만 담고, 원문을 그대로 옮기지 말고 요약하세요. 한국어로 작성합니다.";
        var user = "[소설 원고]\n" + Shorten(manuscript, 12000) + "\n\n[요청] 이 원고의 전체 시놉시스를 작성하세요.";
        return await EnsureKoreanAsync(await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) }));
    }

    /// <summary>
    /// 원고를 분석해 장 구성을 추출합니다.
    /// </summary>
    public async Task<List<ChapterNode>> ExtractChaptersAsync(string manuscript)
    {
        const string system = "당신은 소설 구조 분석가입니다. 요청한 JSON 배열만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 JSON 문자열 값은 반드시 한국어로 작성하고 영어를 쓰지 마세요.";
        var user = "[소설 원고]\n" + Shorten(manuscript, 14000)
            + "\n\n[요청] 위 원고를 이야기 흐름에 따라 장으로 나누고, 각 장을 JSON 배열의 원소로 출력하세요. 각 원소 키: "
            + "\"title\",\"summary\",\"purpose\",\"conflict\",\"reveal\",\"ending\". JSON 배열만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var chapters = new List<ChapterNode>();
        foreach (var element in EnumerateJsonArray(reply))
        {
            chapters.Add(new ChapterNode
            {
                Title = await EnsureKoreanAsync(GetString(element, "title")),
                Summary = await EnsureKoreanAsync(GetString(element, "summary")),
                Purpose = await EnsureKoreanAsync(GetString(element, "purpose")),
                Conflict = await EnsureKoreanAsync(GetString(element, "conflict")),
                Reveal = await EnsureKoreanAsync(GetString(element, "reveal")),
                Ending = await EnsureKoreanAsync(GetString(element, "ending"))
            });
        }

        return chapters;
    }

    // ────────────────────────── 한글 강제 후처리 ──────────────────────────

    // 3글자 이상 연속된 영단어를 감지합니다. (약어/짧은 기호는 무시)
    private static readonly Regex EnglishWordRegex = new("[A-Za-z]{3,}", RegexOptions.Compiled);

    // Scene 편집 항목이 본문에 그대로 새어 나온 줄(예: "목표:", "**등장인물**:", "- 장소:")을 감지합니다.
    // 앞에 붙는 글머리표(*, -, 숫자., #)와 굵게(**)를 허용합니다.
    private static readonly Regex SceneMetaLineRegex = new(
        @"^\s*[\*\#\-\d\.\)\s]*\**\s*(제목|요약|목표|목적|등장\s*인물|인물|장소|배경|갈등|결과|시작\s*상태|시작|종료\s*상태|종료|반전|다음\s*(Scene|씬|장면)?\s*연결|비트|Scene|씬|장면)\s*\**\s*[:：]",
        RegexOptions.Compiled);

    /// <summary>
    /// 본문에 섞여 나온 Scene 설정 항목 줄(제목/목표/등장인물/장소/갈등/결과 등)을 제거합니다.
    /// </summary>
    private static string StripSceneMeta(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var kept = lines.Where(line => !SceneMetaLineRegex.IsMatch(line));
        return string.Join("\n", kept).Trim();
    }

    /// <summary>
    /// 텍스트에 영어가 섞여 있으면 한국어로 재번역합니다. (없으면 그대로 반환 · AI 호출 없음)
    /// </summary>
    private async Task<string> EnsureKoreanAsync(string? text)
    {
        var value = text ?? string.Empty;
        if (!EnglishWordRegex.IsMatch(value))
        {
            return value;
        }

        const string system =
            "다음 텍스트를 자연스러운 한국어로 옮기세요. 이미 한국어인 부분은 그대로 두고 영어로 된 부분만 한국어로 번역하세요. "
            + "인명·지명 같은 고유명사는 한글 표기로 바꾸되 의미는 유지하세요. 번역 결과만 출력하고 다른 설명은 쓰지 마세요.";
        var translated = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", value) });
        return string.IsNullOrWhiteSpace(translated) ? value : translated.Trim();
    }

    private static JsonElement? ParseJsonObject(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return null;
        }

        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(reply[start..(end + 1)]);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildBible(StoryProject p)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[작품 설정]");
        if (!string.IsNullOrWhiteSpace(p.Title)) sb.AppendLine($"제목: {p.Title}");
        if (!string.IsNullOrWhiteSpace(p.Genre)) sb.AppendLine($"장르: {p.Genre}");
        if (!string.IsNullOrWhiteSpace(p.Era)) sb.AppendLine($"시대: {p.Era}");
        if (!string.IsNullOrWhiteSpace(p.World)) sb.AppendLine($"세계관: {p.World}");
        if (!string.IsNullOrWhiteSpace(p.CoreEvent)) sb.AppendLine($"핵심 사건: {p.CoreEvent}");
        if (!string.IsNullOrWhiteSpace(p.Ending)) sb.AppendLine($"결말: {p.Ending}");
        if (!string.IsNullOrWhiteSpace(p.Forbidden)) sb.AppendLine($"금지사항: {p.Forbidden}");

        if (p.Characters.Count > 0)
        {
            sb.AppendLine("등장인물:");
            foreach (var c in p.Characters)
            {
                sb.AppendLine($"  - {c.Name} / 성격: {c.Personality} / 목표: {c.Goal} / 비밀: {c.Secret} / 관계: {c.Relationships}");
            }
        }

        if (!string.IsNullOrWhiteSpace(p.ReferenceNotes))
        {
            sb.AppendLine("[참고자료]");
            sb.AppendLine(Shorten(p.ReferenceNotes, 2500));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 참고자료(마크다운)에서 등장인물 1명의 정보를 추출합니다.
    /// </summary>
    public async Task<StoryCharacter?> ExtractCharacterAsync(string markdown)
    {
        const string system = "당신은 소설 분석가입니다. 요청한 JSON 객체만 출력하고 다른 설명은 절대 쓰지 마세요. 모든 값은 반드시 한국어로 작성하세요.";
        var user = "[참고자료]\n" + Shorten(markdown, 6000)
            + "\n\n[요청] 이 자료에 나온 등장인물 1명의 정보를 JSON 객체로 출력하세요. 키: "
            + "\"name\"(이름), \"personality\"(성격), \"goal\"(목표), \"secret\"(비밀), \"relationships\"(관계). JSON 객체만 출력하세요.";

        var reply = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
        var root = ParseJsonObject(reply);
        if (root is null)
        {
            return null;
        }

        return new StoryCharacter
        {
            Name = await EnsureKoreanAsync(GetString(root.Value, "name")),
            Personality = await EnsureKoreanAsync(GetString(root.Value, "personality")),
            Goal = await EnsureKoreanAsync(GetString(root.Value, "goal")),
            Secret = await EnsureKoreanAsync(GetString(root.Value, "secret")),
            Relationships = await EnsureKoreanAsync(GetString(root.Value, "relationships"))
        };
    }

    private static string Shorten(string? text, int max)
    {
        text ??= string.Empty;
        return text.Length <= max ? text : text[..max] + "…";
    }

    /// <summary>
    /// 긴 원고를 청크별로 요약해 압축본을 만듭니다. (장편 대응 · 맵 단계)
    /// 짧은 원고는 그대로 반환합니다.
    /// </summary>
    /// <param name="manuscript">원본 원고입니다.</param>
    /// <param name="progress">진행 상황 보고(부분 요약 n/m)입니다.</param>
    public async Task<string> CondenseAsync(string? manuscript, IProgress<string>? progress = null)
    {
        var text = manuscript ?? string.Empty;
        const int threshold = 12000;
        if (text.Length <= threshold)
        {
            return text;
        }

        var chunks = SplitIntoChunks(text, 9000);
        var summaries = new List<string>();

        for (var i = 0; i < chunks.Count; i++)
        {
            progress?.Report($"긴 원고 부분 요약 {i + 1}/{chunks.Count}...");

            const string system =
                "당신은 소설 분석가입니다. 주어진 원고 조각을 사건 중심으로 6~10문장으로 요약하세요. "
                + "등장인물 이름, 주요 사건, 장소, 시간 흐름을 반드시 유지하세요. 한국어로 작성합니다.";
            var user = $"[원고 {i + 1}부]\n{chunks[i]}\n\n[요청] 이 부분을 요약하세요.";

            var summary = await _chat.AskAsync(new[] { new ChatTurn("system", system), new ChatTurn("user", user) });
            if (!string.IsNullOrWhiteSpace(summary))
            {
                summaries.Add($"[{i + 1}부]\n{summary}");
            }
        }

        return summaries.Count > 0 ? string.Join("\n\n", summaries) : Shorten(text, threshold);
    }

    private static List<string> SplitIntoChunks(string text, int maxLength)
    {
        var chunks = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n");
        var builder = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (builder.Length > 0 && builder.Length + paragraph.Length > maxLength)
            {
                chunks.Add(builder.ToString());
                builder.Clear();
            }

            builder.Append(paragraph).Append("\n\n");

            // 단일 문단이 지나치게 길면 강제로 분할합니다.
            while (builder.Length > maxLength * 3 / 2)
            {
                chunks.Add(builder.ToString(0, maxLength));
                builder.Remove(0, maxLength);
            }
        }

        if (builder.Length > 0)
        {
            chunks.Add(builder.ToString());
        }

        return chunks;
    }

    private static IEnumerable<JsonElement> EnumerateJsonArray(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            yield break;
        }

        var start = reply.IndexOf('[');
        var end = reply.LastIndexOf(']');
        if (start < 0 || end <= start)
        {
            yield break;
        }

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(reply[start..(end + 1)]);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                yield return element.Clone();
            }
        }
    }

    private static string GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }
}
