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

    /// <summary>얕은 복사본을 만듭니다.</summary>
    public ImageStyleSettings Clone() => new()
    {
        Preset = Preset,
        Quality = Quality,
        Lighting = Lighting,
        ColorMood = ColorMood,
        ExtraPositive = ExtraPositive,
        ExtraNegative = ExtraNegative
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

    /// <summary>
    /// 스타일 설정에서 이미지 프롬프트에 붙일 긍정 접두(스타일)를 조합합니다.
    /// </summary>
    public static string BuildPositivePrefix(ImageStyleSettings s)
    {
        var parts = new[]
        {
            FindPreset(s.Preset).Positive,
            LookupQ(Qualities, s.Quality),
            LookupQ(Lightings, s.Lighting),
            LookupQ(ColorMoods, s.ColorMood),
            s.ExtraPositive?.Trim() ?? string.Empty
        };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>
    /// 스타일 설정에서 부정 프롬프트를 조합합니다. (기본 + 프리셋 + 사용자 추가)
    /// </summary>
    public static string BuildNegative(ImageStyleSettings s)
    {
        var presetNeg = FindPreset(s.Preset).Negative ?? string.Empty;
        var parts = new[] { BaseNegative, presetNeg, s.ExtraNegative?.Trim() ?? string.Empty };
        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string LookupQ(IReadOnlyList<(string Label, string Positive)> list, string label)
        => list.FirstOrDefault(x => x.Label == label).Positive ?? string.Empty;
}
