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

    public static string GenerateFramedText(string text, int maxWidth = 35)
    {
        // Минимальная ширина для рамки (минимум 1 символ текста + 2 пробела + 2 рамки = 5)
        // То есть innerContentWidth должно быть хотя бы 1, а frameWidth хотя бы 5
        if (maxWidth < 5) 
        {
            maxWidth = 5; // Устанавливаем минимальную ширину рамки
        }

        // Ширина внутреннего содержимого (без пробелов и символов рамки)
        // 2 пробела по бокам + 2 вертикальные линии рамки
        int innerContentWidth = maxWidth - 4; // text.Length + 2 spaces = maxWidth - 2 frame chars

        // Если innerContentWidth становится <= 0, это значит, что maxWidth слишком мал для текста с отступами и рамкой.
        // В этом случае, делаем innerContentWidth минимальным для корректной работы.
        if (innerContentWidth <= 0) {
            innerContentWidth = 1; // Минимальная внутренняя ширина для текста
            maxWidth = innerContentWidth + 4; // Обновляем maxWidth
        }


        // Разбиваем текст на строки, учитывая innerContentWidth
        List<string> wrappedLines = WrapText(text, innerContentWidth);

        // Общая ширина рамки, включая углы
        int frameTotalWidth = maxWidth;

        // Создаем верхнюю и нижнюю границы рамки
        string horizontalLine = new string('═', frameTotalWidth - 2); // '═' без углов
        string topLine = "╔" + horizontalLine + "╗";
        string bottomLine = "╚" + horizontalLine + "╝";

        // Создаем строку с пустыми пробелами для отступа
        string emptyLineContent = new string(' ', frameTotalWidth - 2);
        string emptyLine = "║" + emptyLineContent + "║";

        // Объединяем все части в одну строку с переносами строк
        StringBuilder framedTextBuilder = new StringBuilder();
        framedTextBuilder.AppendLine(topLine);
        framedTextBuilder.AppendLine(emptyLine); // Пустая строка для отступа

        foreach (string line in wrappedLines)
        {
            // Отцентрировать текст или выровнять по левому краю с пробелами
            // Для простоты, пока выравниваем по левому краю с 2 пробелами
            string paddedLine = line.PadRight(innerContentWidth); // Добавляем пробелы до innerContentWidth
            framedTextBuilder.AppendLine("║  " + paddedLine + "  ║");
        }

        framedTextBuilder.AppendLine(emptyLine); // Пустая строка для отступа
        framedTextBuilder.AppendLine(bottomLine);

        return framedTextBuilder.ToString();
    }