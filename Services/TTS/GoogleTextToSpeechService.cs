using System.Text.Json;
using System.Net.Http.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services.TTS;

public class GoogleTextToSpeechService : ITextToSpeechService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string? _accessToken;
    private readonly string? _project;

    public GoogleTextToSpeechService(HttpClient httpClient)
    {
        _http = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("GOOGLE_TTS_API_KEY");
        _accessToken = Environment.GetEnvironmentVariable("GOOGLE_TTS_ACCESS_TOKEN");
        _project = Environment.GetEnvironmentVariable("GOOGLE_TTS_PROJECT");
        if (string.IsNullOrEmpty(_apiKey) && string.IsNullOrEmpty(_accessToken))
        {
            throw new InvalidOperationException("GOOGLE_TTS_API_KEY or GOOGLE_TTS_ACCESS_TOKEN must be set.");
        }
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string languageCode, string voiceName, double speed)
    {
        var request = new
        {
            input = new { text },
            voice = new { languageCode, name = voiceName },
            audioConfig = new { audioEncoding = "OGG_OPUS", speakingRate = speed }
        };

        var url = "https://texttospeech.googleapis.com/v1/text:synthesize";
        if (!string.IsNullOrEmpty(_apiKey))
        {
            url += $"?key={_apiKey}";
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrEmpty(_accessToken))
        {
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }

        if (!string.IsNullOrEmpty(_project))
        {
            httpRequest.Headers.Add("X-Goog-User-Project", _project);
        }

        var response = await _http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var audioBase64 = json.GetProperty("audioContent").GetString() ?? string.Empty;
        var bytes = Convert.FromBase64String(audioBase64);
        return new MemoryStream(bytes);
    }
}
