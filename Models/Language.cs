namespace TelegramWordBot.Models;

public class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // Например, "en"
    public string Name { get; set; } = string.Empty; // Например, "English"
}
