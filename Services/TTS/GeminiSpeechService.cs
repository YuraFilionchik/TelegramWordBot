using System.Text.Json;
using System.Net.Http.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services.TTS;

public class GeminiSpeechService : ITextToSpeechService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly Dictionary<string, (string Code, string Voice)> _languageMap;
    private readonly string _defaultCode = "en-US";
    private readonly string _defaultVoice = "en-US-Standard-B";

    public GeminiSpeechService(HttpClient http)
    {
        _http = http;
        _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not set.");
        _languageMap = new Dictionary<string, (string Code, string Voice)>(StringComparer.OrdinalIgnoreCase)
        {
            ["English"] = ("en-US", "en-US-Standard-B"),
            ["Russian"] = ("ru-RU", "ru-RU-Standard-B")
        };
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string language, double speed)
    {
        if (!_languageMap.TryGetValue(language, out var cfg))
        {
            cfg = (_defaultCode, _defaultVoice);
        }

        const string modelId = "gemini-2.5-pro-preview-tts";
        var request = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text } }
                }
            },
            generationConfig = new
            {
                responseModalities = new[] { "audio" },
                temperature = 1,
                speechConfig = new
                {
                    voiceConfig = new
                    {
                        prebuiltVoiceConfig = new { voiceName = cfg.Voice }
                    },
                    languageCode = cfg.Code
                }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelId}:generateContent?key={_apiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
        };

        var response = await _http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var base64 = json.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("fileData").GetProperty("data").GetString() ?? string.Empty;
        var bytes = Convert.FromBase64String(base64);
        return new MemoryStream(bytes);
    }

    public async Task<string> GetModelsAsync()
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}";
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
