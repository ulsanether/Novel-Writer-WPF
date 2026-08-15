using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// ComfyUI 백엔드를 호출합니다. A1111과 달리 <b>워크플로우(노드 그래프) JSON</b>을 <c>/prompt</c>로 보내고,
/// <c>/history</c>로 완료를 감지한 뒤 <c>/view</c>로 이미지를 회수합니다. 최신 모델(SDXL/SD3.5/FLUX)에 적합합니다.
/// </summary>
public sealed class ComfyUiImageService : IImageBackend
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// <summary>ComfyUI 서버 주소입니다. (기본 포트 8188)</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8188";

    /// <summary>가로 크기입니다.</summary>
    public int Width { get; set; } = 832;

    /// <summary>세로 크기입니다. (SDXL 세로 비율)</summary>
    public int Height { get; set; } = 1216;

    /// <summary>샘플링 스텝입니다.</summary>
    public int Steps { get; set; } = 28;

    /// <summary>CFG 스케일입니다.</summary>
    public double CfgScale { get; set; } = 6.5;

    /// <summary>샘플러 이름입니다. (ComfyUI 표기)</summary>
    public string Sampler { get; set; } = "dpmpp_2m";

    /// <summary>스케줄러입니다.</summary>
    public string Scheduler { get; set; } = "karras";

    /// <summary>공통 네거티브 프롬프트입니다.</summary>
    public string NegativePrompt { get; set; } = "lowres, bad anatomy, bad hands, missing fingers, extra digit, text, watermark, signature, jpeg artifacts";

    /// <summary>사용할 체크포인트 파일명입니다. 비어 있으면 서버에서 첫 번째를 자동 선택합니다.</summary>
    public string CheckpointName { get; set; } = string.Empty;

    /// <summary>
    /// 서버가 실행 중인지 확인합니다.
    /// </summary>
    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await HttpClient.GetAsync($"{Root()}/system_stats", timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 프롬프트로 이미지를 생성합니다. 실패 시 null입니다.
    /// </summary>
    public async Task<ImageGenResult?> GenerateAsync(string prompt, long seed = -1, CancellationToken cancellationToken = default)
    {
        try
        {
            var checkpoint = await ResolveCheckpointAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(checkpoint))
            {
                return null; // 설치된 체크포인트 모델이 없음
            }

            var usedSeed = seed < 0 ? Math.Abs(Random.Shared.NextInt64()) % 999_999_999_999 : seed;
            var workflow = BuildWorkflow(prompt, checkpoint, usedSeed);
            var clientId = Guid.NewGuid().ToString("N");

            var promptId = await QueuePromptAsync(workflow, clientId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(promptId))
            {
                return null;
            }

            var (filename, subfolder, type) = await WaitForImageAsync(promptId, cancellationToken).ConfigureAwait(false);
            if (filename is null)
            {
                return null;
            }

            var bytes = await FetchImageAsync(filename, subfolder ?? string.Empty, type ?? "output", cancellationToken).ConfigureAwait(false);
            return bytes is null ? null : new ImageGenResult(bytes, usedSeed);
        }
        catch
        {
            return null;
        }
    }

    // 사용할 체크포인트를 결정합니다. (지정값 우선, 없으면 서버의 첫 번째)
    private async Task<string?> ResolveCheckpointAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(CheckpointName))
        {
            return CheckpointName;
        }

        try
        {
            using var response = await HttpClient.GetAsync($"{Root()}/object_info/CheckpointLoaderSimple", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            // CheckpointLoaderSimple.input.required.ckpt_name[0] = [파일명 배열]
            var list = doc.RootElement
                .GetProperty("CheckpointLoaderSimple")
                .GetProperty("input")
                .GetProperty("required")
                .GetProperty("ckpt_name")[0];

            if (list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
            {
                var first = list[0].GetString();
                CheckpointName = first ?? string.Empty;
                return first;
            }
        }
        catch
        {
            // 무시
        }

        return null;
    }

    // 기본 txt2img 워크플로우(그래프)를 만듭니다. (SDXL/SD 계열 공용)
    private Dictionary<string, object> BuildWorkflow(string prompt, string checkpoint, long seed)
    {
        return new Dictionary<string, object>
        {
            ["4"] = Node("CheckpointLoaderSimple", new Dictionary<string, object> { ["ckpt_name"] = checkpoint }),
            ["5"] = Node("EmptyLatentImage", new Dictionary<string, object>
            {
                ["width"] = Width,
                ["height"] = Height,
                ["batch_size"] = 1
            }),
            ["6"] = Node("CLIPTextEncode", new Dictionary<string, object>
            {
                ["text"] = prompt,
                ["clip"] = new object[] { "4", 1 }
            }),
            ["7"] = Node("CLIPTextEncode", new Dictionary<string, object>
            {
                ["text"] = NegativePrompt,
                ["clip"] = new object[] { "4", 1 }
            }),
            ["3"] = Node("KSampler", new Dictionary<string, object>
            {
                ["seed"] = seed,
                ["steps"] = Steps,
                ["cfg"] = CfgScale,
                ["sampler_name"] = Sampler,
                ["scheduler"] = Scheduler,
                ["denoise"] = 1.0,
                ["model"] = new object[] { "4", 0 },
                ["positive"] = new object[] { "6", 0 },
                ["negative"] = new object[] { "7", 0 },
                ["latent_image"] = new object[] { "5", 0 }
            }),
            ["8"] = Node("VAEDecode", new Dictionary<string, object>
            {
                ["samples"] = new object[] { "3", 0 },
                ["vae"] = new object[] { "4", 2 }
            }),
            ["9"] = Node("SaveImage", new Dictionary<string, object>
            {
                ["images"] = new object[] { "8", 0 },
                ["filename_prefix"] = "NovelWriter"
            })
        };
    }

    private static Dictionary<string, object> Node(string classType, Dictionary<string, object> inputs)
        => new() { ["class_type"] = classType, ["inputs"] = inputs };

    // 워크플로우를 큐에 넣고 prompt_id를 받습니다.
    private async Task<string?> QueuePromptAsync(Dictionary<string, object> workflow, string clientId, CancellationToken cancellationToken)
    {
        var payload = new { prompt = workflow, client_id = clientId };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root()}/prompt")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return doc.RootElement.TryGetProperty("prompt_id", out var id) ? id.GetString() : null;
    }

    // 완료될 때까지 history를 폴링하고 첫 출력 이미지 정보를 반환합니다.
    private async Task<(string? filename, string? subfolder, string? type)> WaitForImageAsync(string promptId, CancellationToken cancellationToken)
    {
        // 최대 약 5분 대기 (2초 간격)
        for (var i = 0; i < 150; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await HttpClient.GetAsync($"{Root()}/history/{promptId}", cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (doc.RootElement.TryGetProperty(promptId, out var entry) &&
                        entry.TryGetProperty("outputs", out var outputs))
                    {
                        foreach (var node in outputs.EnumerateObject())
                        {
                            if (node.Value.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                            {
                                var img = images[0];
                                return (
                                    img.TryGetProperty("filename", out var f) ? f.GetString() : null,
                                    img.TryGetProperty("subfolder", out var s) ? s.GetString() : string.Empty,
                                    img.TryGetProperty("type", out var t) ? t.GetString() : "output");
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // 일시 오류는 무시하고 재시도
            }

            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
        }

        return (null, null, null);
    }

    // /view로 실제 PNG 바이트를 가져옵니다.
    private async Task<byte[]?> FetchImageAsync(string filename, string subfolder, string type, CancellationToken cancellationToken)
    {
        var url = $"{Root()}/view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
        using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private string Root() => BaseUrl.TrimEnd('/');
}
