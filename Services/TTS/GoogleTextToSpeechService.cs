using System.Text.Json;
using System.Net.Http.Json;
using System.Globalization;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services.TTS;

public class GoogleTextToSpeechService : ITextToSpeechService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string? _accessToken;
    private readonly string? _project;
    private readonly Dictionary<string, (string Code, string Voice)> _languageMap;
    private readonly string _defaultCode = "en-US";
    private readonly string _defaultVoice = "en-US-Standard-B";
    private bool _languagesLoaded;

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

        _languageMap = new Dictionary<string, (string Code, string Voice)>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Stream> SynthesizeSpeechAsync(string text, string language, double speed)
    {
        await EnsureLanguagesLoadedAsync();

        if (!_languageMap.TryGetValue(language, out var cfg))
        {
            cfg = (_defaultCode, _defaultVoice);
        }

        var request = new
        {
            input = new { text },
            voice = new { languageCode = cfg.Code, name = cfg.Voice },
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

    private async Task EnsureLanguagesLoadedAsync()
    {
        if (_languagesLoaded)
            return;

        try
        {
            var url = "https://texttospeech.googleapis.com/v1/voices";
            if (!string.IsNullOrEmpty(_apiKey))
            {
                url += $"?key={_apiKey}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(_accessToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            }

            if (!string.IsNullOrEmpty(_project))
            {
                request.Headers.Add("X-Goog-User-Project", _project);
            }

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("voices", out var voices))
            {
                foreach (var voice in voices.EnumerateArray())
                {
                    if (!voice.TryGetProperty("name", out var nameProp))
                        continue;
                    var voiceName = nameProp.GetString() ?? string.Empty;
                    if (!voice.TryGetProperty("languageCodes", out var codesProp))
                        continue;

                    foreach (var codeElement in codesProp.EnumerateArray())
                    {
                        var code = codeElement.GetString();
                        if (string.IsNullOrEmpty(code))
                            continue;

                        if (!_languageMap.ContainsKey(code))
                        {
                            _languageMap[code] = (code, voiceName);
                        }

                        try
                        {
                            var name = CultureInfo.GetCultureInfo(code).EnglishName.Split('(')[0].Trim();
                            if (!_languageMap.ContainsKey(name))
                            {
                                _languageMap[name] = (code, voiceName);
                            }
                        }
                        catch (CultureNotFoundException)
                        {
                        }
                    }
                }
            }

            _languagesLoaded = true;
        }
        catch
        {
            if (_languageMap.Count == 0)
            {
                _languageMap[_defaultCode] = (_defaultCode, _defaultVoice);
            }
        }
    }
}

