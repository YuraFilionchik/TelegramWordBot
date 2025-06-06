namespace TelegramWordBot.Models;

public class TodoItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Created_At { get; set; }
    public bool Is_Complete { get; set; } = false;
}
