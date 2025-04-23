
namespace TelegramWordBot.Models
{
 public  class Translation
    {
        public Guid Id { get; set; }
        public Guid WordId { get; set; }
        public int LanguageId { get; set; }
        public string Text { get; set; }
        public string? Examples { get; set; }

    }
}
