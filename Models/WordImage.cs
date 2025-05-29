using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramWordBot.Models
{
    public class WordImage
    {
        public Guid Id { get; set; }
        public Guid WordId { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
