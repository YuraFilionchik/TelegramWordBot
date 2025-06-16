using System.Text.Json;
using System.Net.Http.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services.TTS;

public class GoogleTextToSpeechService : ITextToSpeechService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GoogleTextToSpeechService(HttpClient httpClient)
    {
        _http = httpClient;
        _apiKey = Environment.GetEnvironmentVariable("GOOGLE_TTS_API_KEY")
            ?? throw new InvalidOperationException("GOOGLE_TTS_API_KEY is not set.");
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string languageCode, string voiceName, double speed)
    {
        var request = new
        {
            input = new { text },
            voice = new { languageCode, name = voiceName },
            audioConfig = new { audioEncoding = "OGG_OPUS", speakingRate = speed }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            $"https://texttospeech.googleapis.com/v1/text:synthesize?key={_apiKey}")
        {
            Content = JsonContent.Create(request)
        };

        var response = await _http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var audioBase64 = json.GetProperty("audioContent").GetString() ?? string.Empty;
        var bytes = Convert.FromBase64String(audioBase64);
        return new MemoryStream(bytes);
    }
}
