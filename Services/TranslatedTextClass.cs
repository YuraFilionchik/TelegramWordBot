using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks; 

namespace TelegramWordBot.Services 
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;

    public class TranslatedItem
    {
        public string? OriginalText { get; set; }
        public string? TranslatedText { get; set; }
        public string? Example { get; set; }
        public string? Error { get; set; }
        public string? OriginalLanguage { get; set; }
        public string? TranslatedLanguage { get; set; }
    }



    public class TranslatedTextClass
    {
        public List<TranslatedItem> Items { get; } = new();
        public string? Error { get; private set; }
        public static string JSONPropertyTranslatedText = "translatedText";
        public static string JSONPropertyOriginalText = "originalText";
        public static string JSONPropertyTranslations = "translations";
        public static string JSONPropertyExample = "example";
        public static string JSONPropertyError = "error";


        public TranslatedTextClass(string json)
        {
            json = json?.Trim().Trim('`') ?? "";
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                Error = "Invalid JSON format";
                return;
            }
            json = json.Substring(start, end - start + 1);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Верхнеуровневая ошибка
                if (root.TryGetProperty(JSONPropertyError, out var topError) && topError.ValueKind == JsonValueKind.String)
                {
                    Error = topError.GetString();
                    return;
                }

                if (!root.TryGetProperty(JSONPropertyTranslations, out var arr) || arr.ValueKind != JsonValueKind.Array)
                {
                    Error = "Missing or invalid 'translations' array.";
                    return;
                }

                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var item = new TranslatedItem();

                    // OriginalText
                    if (el.TryGetProperty(JSONPropertyOriginalText, out var orig) && orig.ValueKind == JsonValueKind.String)
                    {
                        item.OriginalText = orig.GetString();
                    }

                    // TranslatedText
                    if (el.TryGetProperty(JSONPropertyTranslatedText, out var trans) && trans.ValueKind == JsonValueKind.String)
                    {
                        item.TranslatedText = trans.GetString();
                    }

                    // Example
                    if (el.TryGetProperty(JSONPropertyExample, out var ex) && ex.ValueKind == JsonValueKind.String)
                    {
                        var example = ex.GetString();
                        if (!string.IsNullOrWhiteSpace(example))
                            item.Example = example;
                    }

                    // Error внутри элемента
                    if (el.TryGetProperty(JSONPropertyError, out var itErr) && itErr.ValueKind == JsonValueKind.String)
                    {
                        item.Error = itErr.GetString();
                    }

                    // Добавляем элемент если есть хоть что-то
                    if (item.OriginalText != null || item.TranslatedText != null || item.Example != null || item.Error != null)
                        Items.Add(item);
                }

                if (Items.Count == 0 && string.IsNullOrEmpty(Error))
                {
                    Error = "No translations found in response.";
                }
            }
            catch (JsonException je)
            {
                Error = $"JSON parsing failed: {je.Message}";
            }
            catch (Exception ex)
            {
                Error = $"Unexpected error: {ex.Message}";
            }
        }

        public bool IsSuccess()
        {
             return string.IsNullOrEmpty(Error) && Items.Count > 0; 
        }
    }


}