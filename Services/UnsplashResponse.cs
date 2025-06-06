using System.Text.Json.Serialization;

namespace TelegramWordBot.Services
{
    public class UnsplashRandomResponse
    {
        [JsonPropertyName("urls")]
        public UnsplashUrls Urls { get; set; } = null!;
    }

    public class UnsplashUrls
    {
        [JsonPropertyName("regular")]
        public string Regular { get; set; } = string.Empty;
    }

    public class UnsplashSearchResponse
    {
        public List<Result> Results { get; set; }
        public class Result
        {
            public Urls Urls { get; set; }
        }
        public class Urls
        {
            public string Regular { get; set; }
        }
    }
}
