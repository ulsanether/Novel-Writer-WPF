namespace NovelWriter.Wpf.Models;

/// <summary>
/// 이미지 생성 화풍(스타일) 설정입니다. 프리셋(실사/2D/수채화 등) + 세부(품질·조명·색감) + 추가 프롬프트.
/// 한국어 라벨(키)만 저장하고, 실제 영어 프롬프트 조각은 <see cref="ImageStyleCatalog"/>에서 조합합니다.
/// </summary>
public sealed class ImageStyleSettings
{
    /// <summary>화풍 프리셋 라벨입니다. (예: 실사, 2D 애니)</summary>
    public string Preset { get; set; } = "스토리북";

    /// <summary>품질 수준 라벨입니다.</summary>
    public string Quality { get; set; } = "고품질";

    /// <summary>조명 라벨입니다.</summary>
    public string Lighting { get; set; } = "없음";

    /// <summary>색감 라벨입니다.</summary>
    public string ColorMood { get; set; } = "없음";

    /// <summary>사용자가 추가로 넣을 긍정 프롬프트입니다.</summary>
    public string ExtraPositive { get; set; } = string.Empty;

    /// <summary>사용자가 추가로 제외할(부정) 프롬프트입니다.</summary>
    public string ExtraNegative { get; set; } = string.Empty;

    // ── 세부(구도·카메라·분위기·환경) ──

    /// <summary>촬영 범위(샷) 라벨입니다.</summary>
    public string Shot { get; set; } = "자동";

    /// <summary>카메라 각도 라벨입니다.</summary>
    public string CameraAngle { get; set; } = "자동";

    /// <summary>분위기 라벨입니다.</summary>
    public string Mood { get; set; } = "없음";

    /// <summary>배경 라벨입니다.</summary>
    public string Background { get; set; } = "자동";

    /// <summary>시간대 라벨입니다.</summary>
    public string TimeOfDay { get; set; } = "자동";

    /// <summary>콘텐츠 이용 등급 라벨입니다. (등장인물 연령과 별개)</summary>
    public string ContentRating { get; set; } = "전체 이용가";

    /// <summary>인물 수 라벨입니다. (인물 생성일 때만 사용)</summary>
    public string CharacterCount { get; set; } = "자동";

    // ── 핵심 슬라이더(0~100) ──

    /// <summary>현실감입니다. (0 스타일화 ~ 100 실사)</summary>
    public int Realism { get; set; } = 50;

    /// <summary>디테일입니다. (0 단순 ~ 100 정교)</summary>
    public int Detail { get; set; } = 60;

    /// <summary>배경 복잡도입니다. (0 미니멀 ~ 100 복잡)</summary>
    public int BackgroundComplexity { get; set; } = 50;

    /// <summary>얕은 복사본을 만듭니다.</summary>
    public ImageStyleSettings Clone() => new()
    {
        Preset = Preset,
        Quality = Quality,
        Lighting = Lighting,
        ColorMood = ColorMood,
        ExtraPositive = ExtraPositive,
        ExtraNegative = ExtraNegative,
        Shot = Shot,
        CameraAngle = CameraAngle,
        Mood = Mood,
        Background = Background,
        TimeOfDay = TimeOfDay,
        ContentRating = ContentRating,
        Realism = Realism,
        Detail = Detail,
        BackgroundComplexity = BackgroundComplexity,
        CharacterCount = CharacterCount
    };
}

/// <summary>
/// 화풍 프리셋·세부 옵션의 한국어 라벨 ↔ 영어 프롬프트 조각 매핑과 조합 로직입니다.
/// </summary>
public static class ImageStyleCatalog
{
    /// <summary>모든 이미지에 공통으로 들어가는 기본 부정 프롬프트입니다.</summary>
    public const string BaseNegative =
        "lowres, bad anatomy, bad hands, missing fingers, extra digit, text, watermark, signature, jpeg artifacts";

    /// <summary>화풍 프리셋: 라벨 → (긍정, 부정, 추천 해상도/스텝).</summary>
    public static readonly IReadOnlyList<(string Label, string Positive, string Negative, int Width, int Height, int Steps)> Presets = new[]
    {
        ("스토리북", "storybook illustration, soft lighting, detailed", "", 832, 1216, 28),
        ("실사", "photorealistic, realistic, ultra detailed, sharp focus, 8k, natural skin texture", "cartoon, anime, illustration, painting, 3d render, cgi", 896, 1152, 32),
        ("2D 애니", "anime style, 2d, cel shading, clean lineart, vibrant colors", "photorealistic, realistic, 3d, photo", 832, 1216, 28),
        ("반실사", "semi-realistic, detailed illustration, painterly rendering", "flat color, low detail", 832, 1216, 30),
        ("수채화", "watercolor painting, soft washes, delicate, paper texture", "3d render, photo, hard edges", 832, 1216, 30),
        ("유화", "oil painting, textured brushstrokes, rich colors, classical", "flat, digital, low detail", 896, 1152, 32),
        ("만화/코믹", "comic book style, bold ink outlines, halftone shading, dynamic", "photorealistic, soft shading", 832, 1216, 28),
        ("픽셀아트", "pixel art, 8-bit, retro game style", "smooth, realistic, high resolution photo", 768, 768, 24),
        ("3D 렌더", "3d render, octane render, cinematic, physically based rendering", "2d, flat, sketch", 1024, 1024, 30),
        ("동양화", "traditional east asian ink painting, sumi-e, brush strokes", "photo, 3d render", 832, 1216, 28),
        ("지브리풍", "studio ghibli style, hand-drawn anime, soft watercolor background, whimsical, warm", "photorealistic, 3d render, cgi", 1024, 1024, 28),
        ("웹툰풍", "korean webtoon style, manhwa, clean digital art, cel shading, bright", "photorealistic, 3d render, sketch, rough", 832, 1216, 28),
        ("극화체", "detailed realistic anime, semi-realistic manga, cinematic shading", "chibi, simple, flat color", 896, 1152, 30)
    };

    /// <summary>지정 프리셋을 찾습니다. (없으면 첫 번째)</summary>
    public static (string Label, string Positive, string Negative, int Width, int Height, int Steps) FindPreset(string label)
        => Presets.FirstOrDefault(p => p.Label == label) is { Label: not null } hit ? hit : Presets[0];

    /// <summary>품질: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> Qualities = new[]
    {
        ("보통", ""),
        ("고품질", "high detail, best quality"),
        ("초고품질", "masterpiece, best quality, ultra detailed, 8k")
    };

    /// <summary>조명: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> Lightings = new[]
    {
        ("없음", ""),
        ("부드러운", "soft lighting"),
        ("영화적", "cinematic lighting, dramatic"),
        ("역광", "backlight, rim light"),
        ("황금빛", "golden hour, warm sunlight"),
        ("밤/네온", "night, neon lighting")
    };

    /// <summary>색감: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> ColorMoods = new[]
    {
        ("없음", ""),
        ("따뜻한", "warm color palette"),
        ("차가운", "cool color palette"),
        ("파스텔", "pastel colors"),
        ("생생한", "vivid saturated colors"),
        ("어둡고 무거운", "dark moody, low key"),
        ("흑백", "monochrome, black and white")
    };

    /// <summary>프리셋 라벨 목록입니다. (UI 바인딩용)</summary>
    public static IReadOnlyList<string> PresetLabels { get; } = Presets.Select(p => p.Label).ToArray();

    /// <summary>품질 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> QualityLabels { get; } = Qualities.Select(p => p.Label).ToArray();

    /// <summary>조명 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> LightingLabels { get; } = Lightings.Select(p => p.Label).ToArray();

    /// <summary>색감 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> ColorMoodLabels { get; } = ColorMoods.Select(p => p.Label).ToArray();

    /// <summary>촬영 범위(샷): 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> Shots = new[]
    {
        ("자동", ""),
        ("얼굴 클로즈업", "extreme close-up, face focus"),
        ("클로즈업", "close-up shot"),
        ("상반신", "upper body, bust shot"),
        ("무릎 위", "cowboy shot, knee up"),
        ("전신", "full body shot"),
        ("와이드샷", "wide shot")
    };

    /// <summary>카메라 각도: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> CameraAngles = new[]
    {
        ("자동", ""),
        ("정면", "front view"),
        ("측면", "side view, profile"),
        ("3/4 뷰", "three-quarter view"),
        ("하이앵글", "high angle shot"),
        ("로우앵글", "low angle shot"),
        ("탑뷰", "top-down view, overhead")
    };

    /// <summary>분위기: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> Moods = new[]
    {
        ("없음", ""),
        ("밝음", "bright cheerful mood"),
        ("평화로움", "peaceful serene atmosphere"),
        ("몽환적", "dreamy ethereal atmosphere"),
        ("신비로움", "mysterious mood"),
        ("우울함", "melancholic mood"),
        ("긴장감", "tense suspenseful atmosphere"),
        ("로맨틱", "romantic atmosphere"),
        ("웅장함", "epic grand atmosphere"),
        ("어두움", "dark gloomy mood"),
        ("공포", "horror, eerie atmosphere")
    };

    /// <summary>배경: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> Backgrounds = new[]
    {
        ("자동", ""),
        ("없음", "plain simple background"),
        ("실내", "indoor interior"),
        ("도시", "city, urban background"),
        ("거리", "street background"),
        ("자연", "nature background"),
        ("숲", "forest"),
        ("바다", "ocean, sea"),
        ("산", "mountains"),
        ("판타지", "fantasy landscape")
    };

    /// <summary>시간대: 라벨 → 긍정.</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> TimesOfDay = new[]
    {
        ("자동", ""),
        ("아침", "morning light"),
        ("낮", "daytime"),
        ("골든아워", "golden hour"),
        ("일몰", "sunset"),
        ("밤", "night")
    };

    /// <summary>콘텐츠 이용 등급 라벨입니다. (프롬프트 부정에 영향)</summary>
    public static IReadOnlyList<string> ContentRatingLabels { get; } = new[] { "전체 이용가", "12+", "15+", "18+" };

    /// <summary>인물 수: 라벨 → 긍정. (인물 생성 전용)</summary>
    public static readonly IReadOnlyList<(string Label, string Positive)> CharacterCounts = new[]
    {
        ("자동", ""),
        ("1명", "solo, single person"),
        ("2명", "two people, 2characters"),
        ("3명", "three people, group of three"),
        ("다수", "multiple people, group, crowd")
    };

    /// <summary>인물 수 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> CharacterCountLabels { get; } = CharacterCounts.Select(p => p.Label).ToArray();

    /// <summary>촬영 범위 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> ShotLabels { get; } = Shots.Select(p => p.Label).ToArray();

    /// <summary>카메라 각도 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> CameraAngleLabels { get; } = CameraAngles.Select(p => p.Label).ToArray();

    /// <summary>분위기 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> MoodLabels { get; } = Moods.Select(p => p.Label).ToArray();

    /// <summary>배경 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> BackgroundLabels { get; } = Backgrounds.Select(p => p.Label).ToArray();

    /// <summary>시간대 라벨 목록입니다.</summary>
    public static IReadOnlyList<string> TimeOfDayLabels { get; } = TimesOfDay.Select(p => p.Label).ToArray();

    /// <summary>
    /// 스타일 설정에서 이미지 프롬프트에 붙일 긍정 접두(스타일)를 조합합니다.
    /// </summary>
    public static string BuildPositivePrefix(ImageStyleSettings s)
    {
        var parts = new[]
        {
            FindPreset(s.Preset).Positive,
            LookupQ(Qualities, s.Quality),
            LookupQ(Shots, s.Shot),
            LookupQ(CharacterCounts, s.CharacterCount),
            LookupQ(CameraAngles, s.CameraAngle),
            LookupQ(Lightings, s.Lighting),
            LookupQ(ColorMoods, s.ColorMood),
            LookupQ(Moods, s.Mood),
            LookupQ(Backgrounds, s.Background),
            LookupQ(TimesOfDay, s.TimeOfDay),
            RealismFragment(s.Realism),
            DetailFragment(s.Detail),
            BackgroundComplexityFragment(s.BackgroundComplexity),
            ContentRatingPositive(s.ContentRating),
            s.ExtraPositive?.Trim() ?? string.Empty
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    // 콘텐츠 이용 등급에 따른 긍정 프롬프트. (18+는 성인물을 명시적으로 유도 — 명시적 태그 포함)
    private static string ContentRatingPositive(string rating)
        => rating == "18+"
            ? "nsfw, explicit, adult content, mature, uncensored, nude, completely nude, exposed breasts, exposed genitalia, sexual content"
            : string.Empty;

    private static string RealismFragment(int v)
        => v <= 25 ? "stylized, artistic" : v >= 75 ? "photorealistic, lifelike detail" : string.Empty;

    private static string DetailFragment(int v)
        => v >= 75 ? "intricate details, sharp focus" : v <= 20 ? "simple, clean" : string.Empty;

    private static string BackgroundComplexityFragment(int v)
        => v <= 25 ? "minimal background" : v >= 75 ? "highly detailed complex background" : string.Empty;

    /// <summary>
    /// 스타일 설정에서 부정 프롬프트를 조합합니다. (기본 + 프리셋 + 사용자 추가)
    /// </summary>
    public static string BuildNegative(ImageStyleSettings s)
    {
        var presetNeg = FindPreset(s.Preset).Negative ?? string.Empty;
        var parts = new[] { BaseNegative, presetNeg, ContentRatingNegative(s.ContentRating), s.ExtraNegative?.Trim() ?? string.Empty };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    // 콘텐츠 이용 등급에 따른 부정 프롬프트. (18+는 제한 없음)
    private static string ContentRatingNegative(string rating) => rating switch
    {
        "전체 이용가" => "nsfw, nude, sexual content, gore, blood, violence",
        "12+" => "nsfw, nude, sexual content, gore",
        "15+" => "nsfw, nude, explicit content",
        _ => string.Empty
    };

    private static string LookupQ(IReadOnlyList<(string Label, string Positive)> list, string label)
        => list.FirstOrDefault(x => x.Label == label).Positive ?? string.Empty;
}
