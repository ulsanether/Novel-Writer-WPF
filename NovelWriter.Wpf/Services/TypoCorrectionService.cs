using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 오타 제안 한 건(틀린 표현 → 고친 표현)을 나타냅니다.
/// </summary>
/// <param name="Wrong">텍스트에 실제로 등장하는 틀린 표현입니다.</param>
/// <param name="Right">교정 제안입니다.</param>
public sealed record TypoPair(string Wrong, string Right);

/// <summary>
/// 간단한 AI 형태 오타 보정 기능을 제공합니다.
/// </summary>
public sealed class TypoCorrectionService
{
    private const string DefaultModel = "gpt-4o-mini";
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// 사용할 모델 이름입니다. 환경변수가 지정되면 그쪽이 우선합니다.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// OpenAI 호환 서버 주소입니다. 환경변수가 지정되면 그쪽이 우선합니다.
    /// </summary>
    public string? BaseUrl { get; set; }

    private static readonly IReadOnlyDictionary<string, string> TypoDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["잇습니다"] = "있습니다",
        ["됬다"] = "됐다",
        ["않돼"] = "안 돼",
        ["업습니다"] = "없습니다",
        ["teh"] = "the",
        ["wirte"] = "write"
    };

    /// <summary>
    /// 텍스트에서 오타를 찾아 (틀린 표현 → 고친 표현) 제안 목록을 반환합니다. 텍스트를 변경하지는 않습니다.
    /// </summary>
    /// <param name="content">검사할 텍스트입니다.</param>
    /// <returns>오타 제안 목록입니다. 오타가 없으면 빈 목록입니다.</returns>
    public async Task<IReadOnlyList<TypoPair>> DetectAsync(string? content)
    {
        var original = content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
        {
            return Array.Empty<TypoPair>();
        }

        var apiKey = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_API_KEY");
        var baseUrl = ResolveBaseUrl();

        if (!string.IsNullOrWhiteSpace(apiKey) || IsLocalServer(baseUrl))
        {
            const string systemPrompt =
                "You are a Korean/English proofreader. Find typos and spacing mistakes in the user's text. "
                + "Respond ONLY with compact JSON of the form {\"corrections\":[{\"wrong\":\"...\",\"right\":\"...\"}]}. "
                + "Each \"wrong\" MUST be an exact substring copied verbatim from the text. Do not rephrase or add commentary. "
                + "If there are no mistakes, return {\"corrections\":[]}.";

            var reply = await SendChatAsync(systemPrompt, original, apiKey, baseUrl).ConfigureAwait(false);
            var parsed = ParseCorrections(reply, original);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return DetectWithDictionary(original);
    }

    /// <summary>
    /// 텍스트 오타를 비동기 보정합니다. (전체 텍스트를 교정본으로 치환)
    /// </summary>
    /// <param name="content">원본 문서 내용입니다.</param>
    /// <returns>보정된 문서 내용입니다.</returns>
    public async Task<string> CorrectAsync(string? content)
    {
        var original = content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(original))
        {
            return string.Empty;
        }

        var apiKey = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_API_KEY");
        var baseUrl = ResolveBaseUrl();

        // 로컬 서버(Ollama 등)는 API 키가 필요 없으므로 키가 없어도 호출을 시도합니다.
        // 원격(OpenAI 등)은 기존처럼 키가 있을 때만 호출합니다.
        if (!string.IsNullOrWhiteSpace(apiKey) || IsLocalServer(baseUrl))
        {
            const string systemPrompt =
                "You are a writing assistant. Fix only typos and spacing mistakes in Korean or English text. "
                + "Preserve meaning, style, and line breaks. Return corrected text only.";

            var corrected = await SendChatAsync(systemPrompt, original, apiKey, baseUrl).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(corrected))
            {
                return corrected;
            }
        }

        var result = original;
        foreach (var pair in TypoDictionary)
        {
            result = result.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// OpenAI 호환 Chat Completions API에 요청을 보내고 assistant 응답 텍스트를 반환합니다.
    /// </summary>
    private async Task<string?> SendChatAsync(string systemPrompt, string userContent, string? apiKey, string baseUrl)
    {
        try
        {
            var model = ResolveModel();

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var payload = new
            {
                model,
                temperature = 0.1,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userContent }
                }
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
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 모델 응답(JSON)에서 corrections 목록을 파싱합니다. 파싱 실패 시 null을 반환합니다.
    /// </summary>
    private static IReadOnlyList<TypoPair>? ParseCorrections(string? reply, string sourceText)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return null;
        }

        // 모델이 코드펜스 등으로 감싸는 경우를 대비해 첫 '{' ~ 마지막 '}' 구간만 취합니다.
        var start = reply.IndexOf('{');
        var end = reply.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        var jsonText = reply[start..(end + 1)];

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            if (!document.RootElement.TryGetProperty("corrections", out var corrections)
                || corrections.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<TypoPair>();

            foreach (var item in corrections.EnumerateArray())
            {
                if (!item.TryGetProperty("wrong", out var wrongElement)
                    || !item.TryGetProperty("right", out var rightElement))
                {
                    continue;
                }

                var wrong = wrongElement.GetString();
                var right = rightElement.GetString();

                // 'wrong'은 반드시 원문에 실제로 존재하고, 교정 결과와 달라야 합니다.
                if (string.IsNullOrEmpty(wrong)
                    || right is null
                    || string.Equals(wrong, right, StringComparison.Ordinal)
                    || !sourceText.Contains(wrong, StringComparison.Ordinal)
                    || !seen.Add(wrong))
                {
                    continue;
                }

                result.Add(new TypoPair(wrong, right));
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// AI를 사용할 수 없을 때 내장 사전으로 오타를 찾습니다.
    /// </summary>
    private static IReadOnlyList<TypoPair> DetectWithDictionary(string text)
    {
        var result = new List<TypoPair>();
        foreach (var pair in TypoDictionary)
        {
            if (text.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(new TypoPair(pair.Key, pair.Value));
            }
        }

        return result;
    }

    /// <summary>
    /// 서버 주소를 우선순위(환경변수 &gt; 설정 &gt; 기본값)에 따라 결정합니다.
    /// </summary>
    private string ResolveBaseUrl()
    {
        var envUrl = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            return envUrl;
        }

        return string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl;
    }

    /// <summary>
    /// 모델 이름을 우선순위(환경변수 &gt; 설정 &gt; 기본값)에 따라 결정합니다.
    /// </summary>
    private string ResolveModel()
    {
        var envModel = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_MODEL");
        if (!string.IsNullOrWhiteSpace(envModel))
        {
            return envModel;
        }

        return string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model;
    }

    /// <summary>
    /// 서버 주소가 로컬 호스트(키가 필요 없는 로컬 추론 서버)인지 판별합니다.
    /// </summary>
    private static bool IsLocalServer(string baseUrl)
    {
        return baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || baseUrl.Contains("0.0.0.0", StringComparison.OrdinalIgnoreCase);
    }
}
