namespace TelegramWordBot.Models;

public class User
{
    public Guid Id { get; set; }
    public long Telegram_Id { get; set; }
    public string NativeLanguage { get; set; } = "Russian";
    public string? CurrentLanguage { get; set; }

}
