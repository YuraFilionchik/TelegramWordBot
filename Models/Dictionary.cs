namespace TelegramWordBot.Models;

public class Dictionary
{
    public Guid Id { get; set; }
    public Guid User_Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
