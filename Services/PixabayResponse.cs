using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TelegramWordBot.Services
{
    public class PixabayResponse
    {
        [JsonPropertyName("hits")]
        public List<PixabayHit> Hits { get; set; }
    }

    public class PixabayHit
    {
        [JsonPropertyName("largeImageURL")]
        public string LargeImageURL { get; set; }
    }
}
