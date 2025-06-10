using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using TelegramWordBot.Models;
using static System.Net.Mime.MediaTypeNames;
using Font = System.Drawing.Font;

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
            var fileName = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text))
                           .Replace("=", "").Replace("/", "_").Replace("+", "-") + ".png";
            var filePath = Path.Combine(AppContext.BaseDirectory, "Resources", fileName);
            if (File.Exists(filePath))
                return filePath;

            // Подбор размера шрифта для одной строки
            string[] lines = null;
            int usedFontSize = fontSize;
            int margin = 3;

            using var bmpTemp = new Bitmap(width, height);
            using var gTemp = Graphics.FromImage(bmpTemp);

            for (int size = fontSize; size >= 12; size--)
            {
                using var fontTest = new Font(fontFamily, size, FontStyle.Regular, GraphicsUnit.Pixel);
                SizeF textSize = gTemp.MeasureString(text, fontTest);

                if (textSize.Width <= width - 2 * margin && textSize.Height <= height - 2 * margin)
                {
                    // Влезает одной строкой
                    usedFontSize = size;
                    lines = new[] { text };
                    break;
                }
            }

            // Если не влезает — word wrap + минимальный размер шрифта
            if (lines == null)
            {
                usedFontSize = 12;
                using var fontTest = new Font(fontFamily, usedFontSize, FontStyle.Regular, GraphicsUnit.Pixel);

                lines = WordWrap(gTemp, text, fontTest, width - 2 * margin);
                // Если по вертикали не помещается — обрезаем строки (или увеличиваем высоту)
                int lineHeight = (int)(fontTest.GetHeight(gTemp) + 4);
                int maxLines = (height - 2 * margin) / lineHeight;
                if (lines.Length > maxLines)
                {
                    var cutLines = new List<string>(lines).GetRange(0, maxLines);
                    // Добавить многоточие на последней строке если текст обрезан
                    cutLines[maxLines - 1] += "...";
                    lines = cutLines.ToArray();
                }
            }

            // создаём основную картинку
            using var bmp = new Bitmap(width, height);
            using var g = Graphics.FromImage(bmp);
            using var font = new Font(fontFamily, usedFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.Black, 2);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.Clear(Color.White);

            // Рисуем рамку
            g.DrawRectangle(pen, margin, margin, width - 2 * margin, height - 2 * margin);

            // Рисуем текст — центрируем вертикально и горизонтально
            int lineHeightDraw = (int)(font.GetHeight(g) + 4);
            int totalTextHeight = lines.Length * lineHeightDraw;
            int startY = margin + (height - 2 * margin - totalTextHeight) / 2;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                SizeF size = g.MeasureString(line, font);
                float x = (width - size.Width) / 2;
                float y = startY + i * lineHeightDraw;
                g.DrawString(line, font, Brushes.Black, x, y);
            }

            bmp.Save(filePath, ImageFormat.Png);
            return filePath;
        }

        /// <summary>
        /// Делит строку на строки так, чтобы каждая влезла по ширине
        /// </summary>
        static string[] WordWrap(Graphics g, string text, Font font, int maxWidth)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                if (g.MeasureString(testLine, font).Width > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                        lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }
            if (!string.IsNullOrEmpty(currentLine))
                lines.Add(currentLine);

            return lines.ToArray();
        }


    }

}