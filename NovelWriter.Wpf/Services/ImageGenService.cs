using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 이미지 생성 결과입니다.
/// </summary>
/// <param name="ImageBytes">PNG 바이트입니다.</param>
/// <param name="Seed">실제 사용된 시드입니다.</param>
public sealed record ImageGenResult(byte[] ImageBytes, long Seed);

/// <summary>
/// 로컬 이미지 생성 백엔드(AUTOMATIC1111 Stable Diffusion WebUI)의 txt2img API를 호출합니다.
/// 텍스트 LLM(Ollama)과 완전히 분리된 별도 서버입니다.
/// </summary>
public sealed class ImageGenService : IImageBackend
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <summary>이미지 서버 주소입니다. (AUTOMATIC1111 기본 포트)</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:7860";

    /// <summary>샘플링 스텝입니다.</summary>
    public int Steps { get; set; } = 25;

    /// <summary>CFG 스케일입니다.</summary>
    public double CfgScale { get; set; } = 7;

    /// <summary>샘플러 이름입니다.</summary>
    public string Sampler { get; set; } = "DPM++ 2M Karras";

    /// <summary>가로 크기입니다.</summary>
    public int Width { get; set; } = 512;

    /// <summary>세로 크기입니다.</summary>
    public int Height { get; set; } = 768;

    /// <summary>공통 네거티브 프롬프트입니다.</summary>
    public string NegativePrompt { get; set; } = "lowres, bad anatomy, bad hands, missing fingers, extra digit, text, watermark, signature, jpeg artifacts";

    /// <summary>
    /// 이미지 서버가 실행 중인지 확인합니다.
    /// </summary>
    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await HttpClient.GetAsync($"{Root()}/sdapi/v1/sd-models", timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 프롬프트로 이미지를 생성합니다. 실패 시 null을 반환합니다.
    /// </summary>
    /// <param name="prompt">영어 이미지 프롬프트입니다.</param>
    /// <param name="seed">시드입니다(-1이면 랜덤).</param>
    public async Task<ImageGenResult?> GenerateAsync(string prompt, long seed = -1, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                prompt,
                negative_prompt = NegativePrompt,
                steps = Steps,
                cfg_scale = CfgScale,
                sampler_name = Sampler,
                width = Width,
                height = Height,
                seed,
                batch_size = 1
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root()}/sdapi/v1/txt2img")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("images", out var images) || images.GetArrayLength() == 0)
            {
                return null;
            }

            var base64 = images[0].GetString();
            if (string.IsNullOrWhiteSpace(base64))
            {
                return null;
            }

            // "data:image/png;base64,..." 접두가 있으면 제거
            var comma = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
            {
                base64 = base64[(comma + 1)..];
            }

            var bytes = Convert.FromBase64String(base64);
            var usedSeed = TryReadSeed(document.RootElement, seed);
            return new ImageGenResult(bytes, usedSeed);
        }
        catch
        {
            return null;
        }
    }

    // 응답 info(JSON 문자열)에서 실제 seed를 읽습니다.
    private static long TryReadSeed(JsonElement root, long requested)
    {
        try
        {
            if (root.TryGetProperty("info", out var info) && info.ValueKind == JsonValueKind.String)
            {
                using var infoDoc = JsonDocument.Parse(info.GetString() ?? "{}");
                if (infoDoc.RootElement.TryGetProperty("seed", out var seedElement) && seedElement.TryGetInt64(out var s))
                {
                    return s;
                }
            }
        }
        catch
        {
            // 무시
        }

        return requested;
    }

    private string Root() => BaseUrl.TrimEnd('/');
}
