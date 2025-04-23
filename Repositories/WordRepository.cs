using Dapper;
using System;
using System.Linq;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories
{
    public class WordRepository
    {
        private readonly DbConnectionFactory _factory;

        public WordRepository(DbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<Word?> GetByTextAsync(string baseText)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Word>(
                "SELECT * FROM words WHERE LOWER(base_text) = LOWER(@BaseText)",
                new { BaseText = baseText });
        }


        public async Task<bool> WordExistsAsync(string baseText, int? languageId = null)
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT EXISTS (
                   SELECT 1 FROM words 
                   WHERE base_text = @BaseText 
                   " + (languageId.HasValue ? "AND language_id = @LanguageId" : "") + ")";

            return await conn.ExecuteScalarAsync<bool>(sql, new { BaseText = baseText, LanguageId = languageId });
        }

        public async Task<IEnumerable<Word>> GetAllWordsAsync()
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryAsync<Word>("SELECT * FROM Words");
        }

        public async Task AddWordAsync(Word word)
        {
            if (await WordExistsAsync(word.BaseText, word.LanguageId)) return;
            var sql = @"INSERT INTO Words (id, base_text, language_id, last_review, 
                     count_total_view, count_plus, count_minus, progress)
                    VALUES (@Id, @BaseText, @LanguageId, @LastReview, 
                            @CountTotalView, @CountPlus, @CountMinus, @Progress)";

            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(sql, word);
        }

        public async Task RemoveAllWords()
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
            DELETE FROM words";
            await conn.ExecuteAsync(sql);
        }
    }

}
