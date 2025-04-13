using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot
{
    public interface IAIHelper
    {
        Task<string> TranslateWordAsync(string word, string sourceLangCode, string targetLangCode);
        Task<string> SimpleTranslateText(string text, string targetLang);
        Task<string> GetLangInfo(string text);
    }

    class AIHelper: IAIHelper
    {
        private readonly HttpClient _http;
        private readonly string? _openAiKey;
        private readonly string _geminiKey;

        public AIHelper(HttpClient httpClient)
        {
            _http = httpClient;
            _openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");// ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");
            _geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("GEMINI_API_KEY is not set.");
        }
        public async Task<string> TranslateWordAsync(string srcText, string nativeLangName, string targetLangName)
        {
            var oneWord = srcText.Split(' ').Count() == 1;
            string prompt = $"Be a high-class translator. There are several languages ​​- {nativeLangName} and {targetLangName} . " +
                $"Automatically recognize the language of the text and translate into other languages ​​from the list.";
            if (oneWord)
                prompt = $"Give a translation of this word - '{srcText}'." +
               $" If you need several of the most common translation options, " +
               $"as well as one example for each meaning of the translation. ";
            else
                prompt = $"Just translate the text without unnecessary information, text = '{srcText}' ";

            prompt+= $"Make all translations as accurately as possible and " +
                $"in the response give only translation without unnecessary explanations and comments. " +
                $"Your response must be exactly in form: [Target language name]=[translation results]" +
                $"If  recognized language of source text  is not in list - make answer for user in {nativeLangName} language," +
                $" that source text language is not in list of his languages,and give response in form: error=[message for user]"; 

            return await TranslateWithGeminiAsync(prompt, false);
           // return await TranslateWithOpenAIAsync(prompt);
        }

        public async Task<string> SimpleTranslateText(string text, string targetLang)
        {
            string prompt = $"Translate the text into language {targetLang}. " +
                $"The answer should contain only the translation text, without comments. " +
                $"The source text is: {text}";
            return await TranslateWithGeminiAsync(prompt, true);
        }

        
        public async Task<string>GetLangInfo(string text)
        {
            string prompt = $"Extract the language name from the following text: '{text}'." +
                $" Give your answer strictly in the format of one word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";
            return await TranslateWithGeminiAsync(prompt, true);
        }

        private async Task<string> TranslateWithOpenAIAsync(string prompt)
        {
            var request = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                new { role = "user", content = prompt }
            },
                temperature = 0.3
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiKey);
            httpRequest.Content = JsonContent.Create(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var response = await _http.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var result = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return result?.Trim() ?? "";
        }

        private async Task<string> TranslateWithGeminiAsync(string prompt, bool lite)
        {
            var requestBody = new
            {
                contents = new[] {
                new {
                    parts = new[] {
                        new { text = prompt }
                    }
                }
            }
            };
            string url;
            if (lite)  url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent?key={_geminiKey}";
            else
                url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };

            httpRequest.Headers.Add("Accept", "application/json");

            var response = await _http.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return content?.Trim() ?? "";
        }

        

        public static string GetResponse(string input)
        {
            // Simulate AI response generation
            // In a real-world scenario, this would involve calling an AI model or API
            return $"AI Response to: {input}";
        }
    }
}
