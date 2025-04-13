
namespace TelegramWordBot.Models
{
    public class Word
    {
        public Guid Id { get; set; }
        public string BaseText { get; set; }
        public int LanguageId { get; set; }
        public DateTime? LastReview { get; set; }
        public int CountTotalView { get; set; } = 0;
        public int CountPlus { get; set; } = 0;
        public int CountMinus { get; set; } = 0;
        public int Progress { get; set; } = 0;
    }
}
