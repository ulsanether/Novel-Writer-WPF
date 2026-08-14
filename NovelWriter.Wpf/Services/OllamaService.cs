using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 모델 다운로드(pull) 진행 상황을 나타냅니다.
/// </summary>
/// <param name="Status">현재 단계 상태 문자열입니다.</param>
/// <param name="Percent">0~100 사이의 진행률입니다. 알 수 없으면 -1입니다.</param>
public sealed record OllamaPullProgress(string Status, double Percent);

/// <summary>
/// 로컬 Ollama 서버(네이티브 API)와 통신하여 모델 준비 상태를 관리합니다.
/// </summary>
public sealed class OllamaService
{
    // pull은 수 GB 다운로드로 오래 걸리므로 타임아웃을 두지 않습니다.
    private static readonly HttpClient HttpClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>
    /// Ollama 네이티브 API 기본 주소입니다. (예: http://localhost:11434)
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Ollama 서버가 실행 중인지 확인합니다.
    /// </summary>
    public async Task<bool> IsRunningAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await HttpClient.GetAsync($"{Root()}/api/tags", timeout.Token).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 설치된 모델 이름 목록을 반환합니다. (실패 시 빈 목록)
    /// </summary>
    public async Task<IReadOnlyList<string>> ListInstalledModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await HttpClient.GetAsync($"{Root()}/api/tags", timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<string>();
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("models", out var models))
            {
                return Array.Empty<string>();
            }

            var names = new List<string>();
            foreach (var item in models.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name) && name.GetString() is { Length: > 0 } value)
                {
                    names.Add(value);
                }
            }

            return names;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 지정한 모델이 로컬에 이미 설치되어 있는지 확인합니다.
    /// </summary>
    /// <param name="model">모델 이름입니다. (예: exaone3.5:7.8b)</param>
    public async Task<bool> IsModelInstalledAsync(string model, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var response = await HttpClient.GetAsync($"{Root()}/api/tags", timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("models", out var models))
            {
                return false;
            }

            foreach (var item in models.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var name)
                    && string.Equals(name.GetString(), model, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 지정한 모델을 다운로드하며 진행률을 보고합니다.
    /// </summary>
    /// <param name="model">받을 모델 이름입니다.</param>
    /// <param name="progress">진행률 보고 대상입니다.</param>
    /// <param name="cancellationToken">취소 토큰입니다.</param>
    public async Task PullModelAsync(string model, IProgress<OllamaPullProgress> progress, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { name = model, stream = true });
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Root()}/api/pull")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var response = await HttpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;

                var status = root.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString() ?? string.Empty
                    : string.Empty;

                double percent = -1;
                if (root.TryGetProperty("total", out var totalElement)
                    && root.TryGetProperty("completed", out var completedElement)
                    && totalElement.TryGetInt64(out var total)
                    && total > 0
                    && completedElement.TryGetInt64(out var completed))
                {
                    percent = Math.Clamp(completed / (double)total * 100, 0, 100);
                }

                progress.Report(new OllamaPullProgress(status, percent));
            }
            catch (JsonException)
            {
                // 부분 수신 등으로 파싱 실패한 줄은 무시합니다.
            }
        }
    }

    private string Root() => BaseUrl.TrimEnd('/');
}
