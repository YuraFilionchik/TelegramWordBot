namespace TelegramWordBot.Models;

public class User
{
    public Guid Id { get; set; }
    public long Telegram_Id { get; set; }
    public string Native_Language { get; set; } = "Russian";
    public string? Current_Language { get; set; }

}
