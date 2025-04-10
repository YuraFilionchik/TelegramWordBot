namespace TelegramWordBot.Models;

public class UserWordProgress
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public DateTime? LastReview { get; set; }
    public int CountTotalView { get; set; }
    public int CountPlus { get; set; }
    public int CountMinus { get; set; }
    public int Progress { get; set; }
}
