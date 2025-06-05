using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services
{
    public interface IAIHelper
    {
        Task<TranslatedTextClass> TranslateWordAsync(string word, string sourceLangName, string targetLangName);
        Task<string> SimpleTranslateText(string text, string targetLang);
        Task<string> GetLangName(string text);
        Task<string> GetLangName(string text, IEnumerable<Language> languages);
        Task<List<string>> GetVariants(string originalWord, string translatedWord, string target_lang);
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
        public async Task<TranslatedTextClass> TranslateWordAsync(string srcText, string sourceLangName, string targetLangName)
        {
            var oneWord = srcText.Split(' ').Count() == 1;
            string prompt = $"You are an expert translator specializing in {sourceLangName} and {targetLangName}. " +
                $"Translate as accurately and naturally as possible. ";

            if (oneWord)
                prompt += $@"Respond ONLY in JSON format, with no explanations or conversational text. 
            {{
              ""{TranslatedTextClass.JSONPropertyTranslations}"": [
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{srcText}"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": ""..."", ""{TranslatedTextClass.JSONPropertyExample}"": ""..."", ""{TranslatedTextClass.JSONPropertyError}"": null }},
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{srcText}"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": ""..."", ""{TranslatedTextClass.JSONPropertyExample}"": ""..."", ""{TranslatedTextClass.JSONPropertyError}"": null }},
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{srcText}"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": null, ""{TranslatedTextClass.JSONPropertyExample}"": null, ""{TranslatedTextClass.JSONPropertyError}"": ""error_message_if_any"" }}
              ]
            }}" +
                        $" Give 1–2 most relevant translations from {sourceLangName} to {targetLangName} for '{srcText}', each with a short example. If you cannot translate, provide error.";
                        else
                            prompt += $@"Respond ONLY in JSON format, with no explanations or conversational text.
            {{
              ""{TranslatedTextClass.JSONPropertyTranslations}"": [
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{srcText}"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": ""..."", ""{TranslatedTextClass.JSONPropertyExample}"": null, ""{TranslatedTextClass.JSONPropertyError}"": null }},
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{srcText}"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": null, ""{TranslatedTextClass.JSONPropertyExample}"": null, ""{TranslatedTextClass.JSONPropertyError}"": ""error_message_if_any"" }}
              ]
            }}" +
            $"Translate from {sourceLangName} to {targetLangName} the phrase: '{srcText}'. If translation is not possible, provide error.";

            prompt += @" // --- Important: Only return valid JSON in the specified format. Do not include explanations. ---";

            var response = await AskWithGeminiAsync(prompt, false);
            TranslatedTextClass returnedTranslate = new TranslatedTextClass(response);
            return returnedTranslate;
        }


        public async Task<string> SimpleTranslateText(string text, string targetLang)
        {
            string prompt = $"Translate the text into language {targetLang}. " +
                $"The answer should contain only the translation text, without comments. " +
                $"The source text is: {text}";
            return await AskWithGeminiAsync(prompt, true);
        }

        
        public async Task<string>GetLangName(string text)
        {
            string prompt = $"Extract the language name from the following text: '{text}'." +
                $" Give your answer strictly in the format of one Word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";
            return await AskWithGeminiAsync(prompt, true);
        }

        public async Task<string> GetLangName(string text, IEnumerable<Language> languages)
        {
            string prompt = "";
            if (languages == null || languages.Count() == 0) 
             prompt = $"Determine the language name of the following text: '{text}'." +
                $" Give your answer strictly in the format of one originalWord with a capital letter in english. " +
                $"If you can not do it - return only 'error'";
            else
            {
                var langsString = string.Join(", ", languages.Select(x => x.Name));
                prompt = $"Determine one language from ( {langsString} ) of the following text: '{text}'." +
                $" Give your answer strictly in the format of one Word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";

            }

            return await AskWithGeminiAsync(prompt, true);
        }

        public async Task<TranslatedTextClass> GetWordByTheme(string theme, int count,string targetLangName, string sourceLangName = "English")
        {
            string prompt = $"Provide a list of {count} words related to the theme '{theme}' translated from {sourceLangName} to {targetLangName}.";
            prompt += $@"Respond ONLY in JSON format, with no explanations or conversational text. 
            {{
              ""{TranslatedTextClass.JSONPropertyTranslatedText}"": [
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{sourceLangName}_word"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": ""{targetLangName}_word"", ""{TranslatedTextClass.JSONPropertyExample}"": ""..."", ""{TranslatedTextClass.JSONPropertyError}"": null }},
                ...
                {{ ""{TranslatedTextClass.JSONPropertyOriginalText}"": ""{sourceLangName}_word"", ""{TranslatedTextClass.JSONPropertyTranslatedText}"": ""{targetLangName}_word"", ""{TranslatedTextClass.JSONPropertyExample}"": ""..."", ""{TranslatedTextClass.JSONPropertyError}"": null }},
              ]
            }}";
            var response = await AskWithGeminiAsync(prompt, false);
            return new TranslatedTextClass(response);
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

        private async Task<string> AskWithGeminiAsync(string prompt, bool lite)
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

        public async Task<List<string>> GetVariants(string originalWord, string translatedWord, string target_lang)
        {
            //где первый элемент — правильный перевод, а остальные — отвлекающие.
            string prompt = $@"
Ты — ассистент по изучению языков.
Дано слово: «{originalWord}» и его перевод на «{target_lang}» - «{translatedWord}».
Задача: сгенерировать еще три варианта перевода этого слова:
1) Первый элемент массива — единственно верный перевод.
2) Оставшиеся три — правдоподобные, но неверные отвлекающие варианты.
Все четыре варианта должны быть на языке «{target_lang}» и представлять собой одно слово или короткую фразу.
Ответь строго в формате одной строки с разделением вариантов по ';', например:
[правильный перевод; отвлечение1; отвлечение2; отвлечение3].
Без каких-либо пояснений и вспомогательного текста.
";
            var result = await AskWithGeminiAsync(prompt, true);
            if (string.IsNullOrWhiteSpace(result) || result.Split(';').Length != 4)
            {
                return new List<string> { translatedWord, "error", "error", "error" };
            }
            else
            {
                result = result.Trim('[',']','.');
                var variants = result.Split(';').Select(x => x.Trim()).ToList();
                return variants;
            }
        }
    }
}
