using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks; 

namespace TelegramWordBot.Services 
{
    public class TranslatedTextClass
    {
        string? translatedText; 
        string? error;
        List<string>? examples;

        public string? TranslatedText { get => translatedText; set => translatedText = value; }
        public List<string>? Examples { get => examples; set => examples = value; }
        public string? Error { get => error; set => error = value; }

        public TranslatedTextClass(string json)
        {
            examples = new List<string>();
            json = json.Trim().Trim('`');
            var startIndex = json.IndexOf('{');
            var endIndex = json.LastIndexOf('}');
            json = json.Substring(startIndex, endIndex - startIndex + 1);

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String)
                    {
                        this.error = errorElement.GetString();
                        // Если есть ошибка верхнего уровня, остальное не парсим
                        return; 
                    }

                    //Парсим массив translations
                    if (root.TryGetProperty("translations", out JsonElement translationsElement) && translationsElement.ValueKind == JsonValueKind.Array)
                    {
                        bool firstTextFound = false; // Флаг, чтобы взять только первый 'text'
                        foreach (JsonElement translationItem in translationsElement.EnumerateArray())
                        {
                            if (translationItem.ValueKind == JsonValueKind.Object)
                            {
                                // Пытаемся извлечь 'text'
                                if ( translationItem.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
                                {
                                    if (!firstTextFound)
                                    {
                                        this.translatedText = textElement.GetString();
                                        firstTextFound = true; // Устанавливаем флаг, что основной текст найден
                                    }
                                    else
                                    {
                                        this.translatedText +=", " + textElement.GetString();
                                    }
                                }

                                // Пытаемся извлечь 'example' (независимо от 'text')
                                if (translationItem.TryGetProperty("example", out JsonElement exampleElement) && exampleElement.ValueKind == JsonValueKind.String)
                                {
                                    string? exampleValue = exampleElement.GetString();
                                    if (!string.IsNullOrEmpty(exampleValue)) // Добавляем только непустые примеры
                                    {
                                        this.examples ??= new List<string>();
                                        this.examples.Add(exampleValue);
                                    }
                                }

                                // Обработка ошибки *внутри* элемента массива
                                // Если основной текст еще не найден и есть ошибка в элементе
                                if (!firstTextFound && string.IsNullOrEmpty(this.error) && translationItem.TryGetProperty("error", out JsonElement itemErrorElement) && itemErrorElement.ValueKind == JsonValueKind.String)
                                {
                                    this.error = itemErrorElement.GetString();
                                    // Можно решить: прервать парсинг массива или продолжить собирать примеры?
                                    // В данном варианте продолжаем, чтобы собрать все возможные данные
                                }
                            }
                        }
                        // Если после цикла не нашли ни одного 'text', а ошибки не было
                        if (!firstTextFound && string.IsNullOrEmpty(this.error))
                        {
                         this.error = "Invalid response format: No 'text' found in translations array.";
                        }

                        // Если после парсинга массив примеров пуст, устанавливаем его в null для консистентности
                        if (this.examples != null && this.examples.Count == 0)
                        {
                            this.examples = null;
                        }
                    }
                    else
                    {
                        // Если нет поля 'translations' (и нет ошибки), считаем это проблемой формата
                        // Либо это может быть случай одного перевода без массива (нужно уточнять формат)
                        // Если формат ТОЛЬКО с массивом, то это ошибка:
                        if (string.IsNullOrEmpty(this.error)) // Устанавливаем ошибку, только если ее еще нет
                        {
                            this.error = "Invalid response format: Missing or invalid 'translations' array.";
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                // Если JSON некорректный, записываем ошибку
                Console.WriteLine($"JSON Parsing Error: {ex.Message}"); // Логирование для отладки
                this.error = $"Failed to parse JSON response: {ex.Message}";
                // Обнуляем остальные поля на всякий случай
                this.translatedText = null;
                this.examples = null;
            }
            catch (Exception ex) // Ловим другие возможные ошибки
            {
                Console.WriteLine($"Unexpected Error during translation object creation: {ex.Message}"); // Логирование
                this.error = $"An unexpected error occurred while processing the translation: {ex.Message}";
                this.translatedText = null;
                this.examples = null;
            }
        }

        public string GetExampleString()
        {
            StringBuilder sb = new StringBuilder();
            if (Examples != null && Examples.Count != 0)
            {
                foreach (var ex in Examples)
                    sb.AppendLine(ex);                
            }
            return sb.ToString();
        }
        public bool IsSuccess()
        {
            return string.IsNullOrEmpty(this.error) && !string.IsNullOrEmpty(this.translatedText);
        }
    }
}