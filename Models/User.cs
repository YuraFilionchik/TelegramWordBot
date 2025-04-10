namespace TelegramWordBot.Models;

public class User
{
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string NativeLanguage { get; set; } = "ru";
}
