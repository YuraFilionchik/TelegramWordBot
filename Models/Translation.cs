
namespace TelegramWordBot.Models
{
 public  class Translation
    {
        public Guid Id { get; set; }
        public Guid Word_Id { get; set; }
        public int Language_Id { get; set; }
        public string Text { get; set; }
        public string? Examples { get; set; }

    }
}
