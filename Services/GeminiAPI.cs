using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TelegramWordBot.Services
{


    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }

        [JsonPropertyName("generationConfig")]
        public GenerationConfiguration GenerationConfig { get; set; }

        [JsonPropertyName("safetySettings")]
        public List<SafetySetting> SafetySettings { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class GenerationConfiguration
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        // Можно добавить topP, topK, если понадобятся
        // [JsonPropertyName("topP")]
        // public double? TopP { get; set; } // Nullable, если не всегда используется

        // [JsonPropertyName("topK")]
        // public int? TopK { get; set; } // Nullable, если не всегда используется
    }

    public class SafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } // Например, "HARM_CATEGORY_HARASSMENT"

        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } // Например, "BLOCK_NONE"
    }


    // --- Классы для ОБРАБОТКИ ОТВЕТА ---
    // (Основано на типичной структуре ответа Gemini API)

    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; }

        // Могут быть и другие поля, например, promptFeedback
        // [JsonPropertyName("promptFeedback")]
        // public PromptFeedback PromptFeedback { get; set; }
    }

    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; } // Используем тот же класс Content

        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }

    public class SafetyRating // Отличается от SafetySetting в запросе
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("probability")]
        public string Probability { get; set; } // Например, "NEGLIGIBLE"
    }

    
}