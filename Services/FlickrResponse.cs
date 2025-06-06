using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TelegramWordBot.Services
{
    public class FlickrSearchResponse
    {
        [JsonPropertyName("photos")]
        public FlickrPhotos Photos { get; set; }
    }

    public class FlickrPhotos
    {
        [JsonPropertyName("photo")]
        public List<FlickrPhoto> Photo { get; set; }
    }

    public class FlickrPhoto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("server")]
        public string Server { get; set; }

        [JsonPropertyName("secret")]
        public string Secret { get; set; }
    }
}
