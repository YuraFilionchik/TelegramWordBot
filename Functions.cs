using System;
using System.Text;
using System.Collections.Generic;

namespace TelegramWordBot
{
    public class AsciiFrameGenerator
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
        public static string GenerateFramedText(
    string text,
    int maxWidth = 25,
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


    }
}