using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services
{
    public interface IAIHelper
    {
        Task<TranslatedTextClass> TranslateWordAsync(string word, string sourceLangCode, string targetLangCode);
        Task<string> SimpleTranslateText(string text, string targetLang);
        Task<string> GetLangName(string text);
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
        public async Task<TranslatedTextClass> TranslateWordAsync(string srcText, string sourLangName, string targetLangName)
        {
            var oneWord = srcText.Split(' ').Count() == 1;
            string prompt = $"You are an expert translator specializing in {sourLangName} and {targetLangName} . " +
                $"Make all translations as accurately as possible.  ";
                
            if (oneWord)
                prompt += @"Respond ONLY in JSON format like this, with no explanations or conversational text: 
               {
                {
                    translations: [
                    { { text: 'translation_1', example: 'example_sentence_1' } },
                    { { text: 'translation_2_if_needed)', example: 'example_sentence_2' } },
                    { { error: 'error_message_if_it_is' } }
                                    ]
                }
            }" +
            $" Give a translation from {sourLangName} to {targetLangName} of this word - '{srcText}'. " ;
            else
                prompt += @"Respond ONLY in JSON format like this, with no explanations or conversational text: 
               {
                {
                    translations: [
                    { { text: 'translation' } },
                    { { error: 'error_message_if_it_is' } }
                                    ]
                }
            } "+
            $"Translate from {sourLangName} to {targetLangName} the text = '{srcText}' ";

            prompt += @"// --- Important: Ensure the JSON is valid and contains only the requested fields. Your answer is content of json.---";
            var response =  await TranslateWithGeminiAsync(prompt, false);
            TranslatedTextClass returnedTranslate = new TranslatedTextClass(response);
            return returnedTranslate;
           // return await TranslateWithOpenAIAsync(prompt);
        }

        public async Task<string> SimpleTranslateText(string text, string targetLang)
        {
            string prompt = $"Translate the text into language {targetLang}. " +
                $"The answer should contain only the translation text, without comments. " +
                $"The source text is: {text}";
            return await TranslateWithGeminiAsync(prompt, true);
        }

        
        public async Task<string>GetLangName(string text)
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
            var requestBody = new GeminiRequest
            {
                Contents = new List<Content>
            {
                new Content
                {
                    Parts = new List<Part> { new Part { Text = prompt } }
                }
            },
                GenerationConfig = new GenerationConfiguration
                {
                    Temperature = 0.0, // Для точности перевода
                    MaxOutputTokens = 450 // Ограничение для короткого текста
                },
                SafetySettings = new List<SafetySetting>
            {
                // ВАЖНО: Использование BLOCK_NONE отключает защиту.
                // Используйте с осторожностью и пониманием рисков.
                new SafetySetting { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_NONE" }
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
