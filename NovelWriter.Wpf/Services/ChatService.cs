using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 대화 한 턴(역할 + 내용)을 나타냅니다.
/// </summary>
/// <param name="Role">system / user / assistant 중 하나입니다.</param>
/// <param name="Content">메시지 내용입니다.</param>
public sealed record ChatTurn(string Role, string Content);

/// <summary>
/// OpenAI 호환 Chat Completions API로 대화형 질문/대답을 수행합니다. (로컬 EXAONE 등)
/// </summary>
public sealed class ChatService
{
    private const string DefaultModel = "gpt-4o-mini";
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(3) };

    /// <summary>
    /// 사용할 모델 이름입니다. 환경변수가 지정되면 그쪽이 우선합니다.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// OpenAI 호환 서버 주소입니다. 환경변수가 지정되면 그쪽이 우선합니다.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// 대화 히스토리를 보내고 assistant 응답 텍스트를 반환합니다. 실패 시 null입니다.
    /// </summary>
    /// <param name="history">system/user/assistant 턴 목록입니다.</param>
    public async Task<string?> AskAsync(IReadOnlyList<ChatTurn> history)
    {
        var apiKey = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_API_KEY");
        var baseUrl = ResolveBaseUrl();

        // 로컬 서버(Ollama 등)는 API 키가 필요 없습니다.
        if (string.IsNullOrWhiteSpace(apiKey) && !IsLocalServer(baseUrl))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var payload = new
            {
                model = ResolveModel(),
                temperature = 0.7,
                messages = history.Select(t => new { role = t.Role, content = t.Content }).ToArray()
            };

            var json = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var contentElement = choices[0].GetProperty("message").GetProperty("content");
            if (contentElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = contentElement.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch
        {
            return null;
        }
    }

    private string ResolveBaseUrl()
    {
        var envUrl = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            return envUrl;
        }

        return string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl;
    }

    private string ResolveModel()
    {
        var envModel = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_MODEL");
        if (!string.IsNullOrWhiteSpace(envModel))
        {
            return envModel;
        }

        return string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model;
    }

    private static bool IsLocalServer(string baseUrl)
    {
        return baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase);
    }
}
