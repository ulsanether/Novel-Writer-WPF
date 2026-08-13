using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NovelWriter.Wpf.Services;

/// <summary>
/// 간단한 AI 형태 오타 보정 기능을 제공합니다.
/// </summary>
public sealed class TypoCorrectionService
{
    private const string DefaultModel = "gpt-4o-mini";
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private static readonly HttpClient HttpClient = new();

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
    /// 텍스트 오타를 비동기 보정합니다.
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
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var aiResult = await TryCorrectWithAiAsync(original, apiKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(aiResult))
            {
                return aiResult;
            }
        }

        var result = original;
        foreach (var pair in TypoDictionary)
        {
            result = result.Replace(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static async Task<string?> TryCorrectWithAiAsync(string original, string apiKey)
    {
        try
        {
            var baseUrl = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_BASE_URL");
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = DefaultBaseUrl;
            }

            var model = Environment.GetEnvironmentVariable("NOVEL_WRITER_OPENAI_MODEL");
            if (string.IsNullOrWhiteSpace(model))
            {
                model = DefaultModel;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var payload = new
            {
                model,
                temperature = 0.1,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a writing assistant. Fix only typos and spacing mistakes in Korean or English text. Preserve meaning, style, and line breaks. Return corrected text only."
                    },
                    new
                    {
                        role = "user",
                        content = original
                    }
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

            var corrected = contentElement.GetString();
            return string.IsNullOrWhiteSpace(corrected) ? null : corrected;
        }
        catch
        {
            return null;
        }
    }
}
