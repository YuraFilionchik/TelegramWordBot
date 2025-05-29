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
        public string? Text { get; set; }
        public string? Example { get; set; }
        public string? Error { get; set; }
    }

    public class TranslatedTextClass
    {
        /// <summary>
        /// Множество пар (перевод → пример или ошибка на уровне элемента)
        /// </summary>
        public List<TranslatedItem> Items { get; } = new();

        /// <summary>
        /// Общая ошибка парсинга (например, отсутствует массив translations)
        /// </summary>
        public string? Error { get; private set; }

        public TranslatedTextClass(string json)
        {
            // Очищаем обёртки
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

                // Если есть верхнеуровневая ошибка — фиксируем и выходим
                if (root.TryGetProperty("error", out var topError)
                    && topError.ValueKind == JsonValueKind.String)
                {
                    Error = topError.GetString();
                    return;
                }

                // Парсим массив translations
                if (!root.TryGetProperty("translations", out var arr)
                    || arr.ValueKind != JsonValueKind.Array)
                {
                    Error = "Missing or invalid 'translations' array.";
                    return;
                }

                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;

                    var item = new TranslatedItem();

                    // text
                    if (el.TryGetProperty("text", out var txt)
                        && txt.ValueKind == JsonValueKind.String)
                    {
                        item.Text = txt.GetString();
                    }

                    // example
                    if (el.TryGetProperty("example", out var ex)
                        && ex.ValueKind == JsonValueKind.String)
                    {
                        var example = ex.GetString();
                        if (!string.IsNullOrWhiteSpace(example))
                            item.Example = example;
                    }

                    // ошибка внутри элемента
                    if (el.TryGetProperty("error", out var itErr)
                        && itErr.ValueKind == JsonValueKind.String)
                    {
                        item.Error = itErr.GetString();
                    }

                    // Добавляем, даже если только есть ошибка
                    if (item.Text != null || item.Example != null || item.Error != null)
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

        /// <summary>
        /// Признак успешного разбора хотя бы одного перевода без ошибок
        /// </summary>
        public bool IsSuccess()
            => string.IsNullOrEmpty(Error)
               && Items.Exists(i => !string.IsNullOrEmpty(i.Text));

        /// <summary>
        /// Собирает все примеры в одну строку (при необходимости)
        /// </summary>
        public string GetAllExamples()
        {
            var sb = new StringBuilder();
            foreach (var i in Items)
            {
                if (!string.IsNullOrEmpty(i.Example))
                    sb.AppendLine(i.Example);
            }
            return sb.ToString();
        }
    }

}