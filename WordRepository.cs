using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
    using Dapper;

namespace TelegramWordBot
{

    public class Word
    {
        public Guid Id { get; set; }
        public string BaseText { get; set; }
        public int LanguageId { get; set; }
        public DateTime? LastReview { get; set; }
        public int CountTotalView { get; set; }
        public int CountPlus { get; set; }
        public int CountMinus { get; set; }
        public int Progress { get; set; }
    }

    public class WordRepository
    {
        private readonly DbConnectionFactory _factory;

        public WordRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<IEnumerable<Word>> GetAllWordsAsync()
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryAsync<Word>("SELECT * FROM Words");
        }

        public async Task AddWordAsync(Word word)
        {
            var sql = @"INSERT INTO Words (id, base_text, language_id, last_review, 
                     count_total_view, count_plus, count_minus, progress)
                    VALUES (@Id, @BaseText, @LanguageId, @LastReview, 
                            @CountTotalView, @CountPlus, @CountMinus, @Progress)";

            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(sql, word);
        }
    }

}
