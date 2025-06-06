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
}
