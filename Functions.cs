using System;
using System.Collections.Generic;
using System.Text;
using TelegramWordBot.Models;
using static System.Net.Mime.MediaTypeNames;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using Font = SixLabors.Fonts.Font;

namespace TelegramWordBot
{
    public class FrameGenerator
    {
        // Метод для разбиения текста на строки по заданной ширине
        private static List<string> WrapText(string text, int maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(""); // Добавляем пустую строку, если текст пуст
                return lines;
            }

            StringBuilder currentLine = new StringBuilder();
            string[] words = text.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                // Проверяем, поместится ли слово в текущую строку
                // Если текущая строка пуста, добавляем слово без пробела в начале
                // Иначе добавляем пробел перед словом
                if (currentLine.Length == 0)
                {
                    if (word.Length <= maxWidth)
                    {
                        currentLine.Append(word);
                    }
                    else
                    {
                        // Слово само по себе длиннее maxWidth, разбиваем его
                        lines.Add(word.Substring(0, maxWidth));
                        lines.AddRange(WrapText(word.Substring(maxWidth), maxWidth)); // Рекурсивно разбиваем остаток слова
                        currentLine.Clear(); // Очищаем текущую строку, так как слово уже обработано
                    }
                }
                else
                {
                    // Проверяем, поместится ли слово с пробелом
                    if ((currentLine.Length + 1 + word.Length) <= maxWidth)
                    {
                        currentLine.Append(" ").Append(word);
                    }
                    else
                    {
                        // Слово не помещается, начинаем новую строку
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                        // Проверяем, помещается ли слово в новую строку
                        if (word.Length <= maxWidth)
                        {
                            currentLine.Append(word);
                        }
                        else
                        {
                            // Слово само по себе длиннее maxWidth, разбиваем его
                            lines.Add(word.Substring(0, maxWidth));
                            lines.AddRange(WrapText(word.Substring(maxWidth), maxWidth)); // Рекурсивно разбиваем остаток слова
                            currentLine.Clear(); // Очищаем текущую строку, так как слово уже обработано
                        }
                    }
                }
            }

            // Добавляем оставшуюся часть текста как последнюю строку
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        /// <summary>
        /// none", html, markdownv2
        /// </summary>
        /// <param name="text"></param>
        /// <param name="maxWidth"></param>
        /// <param name="format">"none", "html", "markdownv2"</param>
        /// <returns></returns>
        public static string GenerateFramedText(string text, int maxWidth = 25,
                                                string format = "none",        // "none", "html", "markdownv2"
                                                bool useAscii = true           // true = +---+ |, false = ╔═══╗ ║
                                                )
        {
            int padding = 1;
            int innerContentWidth = maxWidth - 2 - 2 * padding;
            if (innerContentWidth < 1)
            {
                innerContentWidth = 1;
                maxWidth = innerContentWidth + 2 + 2 * padding;
            }

            List<string> wrappedLines = WrapText(text, innerContentWidth);

            // Рамочные символы
            string h = useAscii ? "-" : "═";
            string v = useAscii ? "|" : "║";
            string tl = useAscii ? "+" : "╔";
            string tr = useAscii ? "+" : "╗";
            string bl = useAscii ? "+" : "╚";
            string br = useAscii ? "+" : "╝";

            string horizontalLine = new string(h[0], maxWidth - 2);
            string topLine = tl + horizontalLine + tr;
            string bottomLine = bl + horizontalLine + br;
            string emptyLine = v + new string(' ', maxWidth - 2) + v;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(topLine);
            sb.AppendLine(emptyLine);
            foreach (var line in wrappedLines)
            {
                // Центрируем!
                int spaces = innerContentWidth - line.Length;
                int leftPad = spaces / 2;
                int rightPad = spaces - leftPad;
                string centered = new string(' ', leftPad) + line + new string(' ', rightPad);
                sb.AppendLine(v + new string(' ', padding) + centered + new string(' ', padding) + v);
            }
            sb.AppendLine(emptyLine);
            sb.AppendLine(bottomLine);

            string result = sb.ToString();

            switch (format.ToLower())
            {
                case "html":
                    return $"<pre>{result}</pre>";
                case "markdownv2":
                    return $"```\n{result}\n```";
                default:
                    return result;
            }
        }

        public static string GenerateSvgFramedText(string text,
                                                int width = 400,
                                                int height = 100,
                                                int fontSize = 24,
                                                string fontFamily = "monospace"
                                                )
        {
            // Отступы рамки
            int margin = 10;
            // Обрабатываем переносы строк
            string[] lines = text.Split('\n');
            int lineHeight = fontSize + 4;

            // Центрируем текст по вертикали
            int totalTextHeight = lines.Length * lineHeight;
            int startY = (height - totalTextHeight) / 2 + fontSize;

            // Формируем SVG
            var sb = new StringBuilder();
            sb.AppendLine($"<svg width=\"{width}\" height=\"{height}\" xmlns=\"http://www.w3.org/2000/svg\">");
            sb.AppendLine($"  <rect x=\"{margin}\" y=\"{margin}\" width=\"{width - 2 * margin}\" height=\"{height - 2 * margin}\" fill=\"white\" stroke=\"black\" stroke-width=\"2\" rx=\"10\"/>");

            for (int i = 0; i < lines.Length; i++)
            {
                sb.AppendLine(
                    $"  <text x=\"50%\" y=\"{startY + i * lineHeight}\" text-anchor=\"middle\" font-family=\"{fontFamily}\" font-size=\"{fontSize}\" fill=\"black\">{System.Security.SecurityElement.Escape(lines[i])}</text>"
                );
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        public static string GeneratePngFramedText(string text, int width = 100, int height = 50, int fontSize = 16, string fontFamily = "Consolas")
        {
            // Генерация имени файла
            var fileName = Convert.ToBase64String(Encoding.UTF8.GetBytes(text))
                               .Replace("=", "")
                               .Replace("/", "_")
                               .Replace("+", "-") + ".png";
            var resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
            Directory.CreateDirectory(resourcesDir);
            var filePath = Path.Combine(resourcesDir, fileName);
            if (File.Exists(filePath))
                return filePath;

            const int margin = 3;
            string[] lines = null;
            int usedFontSize = fontSize;

            // Подготовка шрифта
            FontFamily family;
            if (!SystemFonts.TryGet(fontFamily, out family) || !SystemFonts.Collection.Families.Any())
            {
                var fontFile = Path.Combine(resourcesDir, $"{fontFamily.ToLowerInvariant()}.ttf"); 
                if (!File.Exists(fontFile))
                    throw new FileNotFoundException($"Font file not found: {fontFile}");
                var fc = new FontCollection();
                family = fc.Add(fontFile);

            }

            // Попытка одной строки
            for (int size = fontSize; size >= 12; size--)
            {
                var fontTest = new Font(family, size);
                var rect = TextMeasurer.MeasureSize(text, new TextOptions(fontTest));
                if (rect.Width <= width - 2 * margin &&
                    rect.Height <= height - 2 * margin)
                {
                    usedFontSize = size;
                    lines = new[] { text };
                    break;
                }
            }

            // Если не влезло — word-wrap при 12px
            if (lines == null)
            {
                usedFontSize = 12;
                var wrapFont = new Font(family, usedFontSize);
                lines = WordWrap(text, wrapFont, width - 2 * margin);

                // Обрезаем по вертикали, добавляя "..." на последней строке
                float lineH = TextMeasurer.MeasureSize("A", new TextOptions(wrapFont)).Height + 4;
                int maxLines = (int)((height - 2 * margin) / lineH);
                if (lines.Length > maxLines)
                {
                    var cut = new List<string>(lines).GetRange(0, maxLines);
                    cut[maxLines - 1] += "...";
                    lines = cut.ToArray();
                }
            }

            // Рисуем изображение
            using var image = new Image<Rgba32>(width, height);
            var drawFont = new Font(family, usedFontSize);
            float lineHeight = TextMeasurer.MeasureSize("A", new TextOptions(drawFont)).Height + 4;
            float totalH = lines.Length * lineHeight;
            float startY = margin + (height - 2 * margin - totalH) / 2;

            image.Mutate(ctx =>
            {
                ctx.Fill(Color.White);
                ctx.Draw(Color.Black, 2, new RectangleF(margin, margin, width - 2 * margin, height - 2 * margin));

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var rect = TextMeasurer.MeasureSize(line, new TextOptions(drawFont));
                    float x = (width - rect.Width) / 2;
                    float y = startY + i * lineHeight;
                    ctx.DrawText(line, drawFont, Color.Black, new PointF(x, y));
                }
            });

            image.SaveAsPng(filePath);
            return filePath;
        }

        private static string[] WordWrap(string text, Font font, float maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var current = new StringBuilder();

            foreach (var w in words)
            {
                var attempt = current.Length == 0 ? w : current + " " + w;
                var rect = TextMeasurer.MeasureSize(attempt, new TextOptions(font));
                if (rect.Width <= maxWidth)
                {
                    if (current.Length > 0) current.Append(' ');
                    current.Append(w);
                }
                else
                {
                    if (current.Length > 0)
                    {
                        lines.Add(current.ToString());
                        current.Clear();
                    }
                    current.Append(w);
                }
            }

            if (current.Length > 0)
                lines.Add(current.ToString());

            return lines.ToArray();
        }
        /// <summary>
        /// Делит строку на строки так, чтобы каждая влезла по ширине
        /// </summary>
        //static string[] WordWrap(Graphics g, string text, Font font, int maxWidth)
        //{
        //    var words = text.Split(' ');
        //    var lines = new List<string>();
        //    var currentLine = "";

        //    foreach (var word in words)
        //    {
        //        var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
        //        if (g.MeasureString(testLine, font).Width > maxWidth)
        //        {
        //            if (!string.IsNullOrEmpty(currentLine))
        //                lines.Add(currentLine);
        //            currentLine = word;
        //        }
        //        else
        //        {
        //            currentLine = testLine;
        //        }
        //    }
        //    if (!string.IsNullOrEmpty(currentLine))
        //        lines.Add(currentLine);

        //    return lines.ToArray();
        //}


    }

}