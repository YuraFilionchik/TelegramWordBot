using TelegramWordBot;
﻿using Dapper;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using TelegramWordBot.Models;
using static System.Net.Mime.MediaTypeNames;

namespace TelegramWordBot.Repositories
{
    public class WordRepository
    {
        private readonly IConnectionFactory _factory;

        public WordRepository(IConnectionFactory factory)
        {
            _factory = factory;
        }

       

        public async Task<Word?> GetByTextAsync(string baseText)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Word>(
                "SELECT * FROM words WHERE LOWER(base_text) = LOWER(@Base_Text)",
                new { Base_Text = baseText });
        }

        public async Task<Word?> GetByTextAndLanguageAsync(string text, int languageId)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Word>(
                "SELECT * FROM words WHERE LOWER(base_text) = LOWER(@Base_Text) AND language_id = @Language_Id",
                new { Base_Text = text, Language_Id = languageId });
        }


        public async Task<bool> WordExistsAsync(string baseText, int? languageId = null)
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT EXISTS (
                   SELECT 1 FROM words 
                   WHERE base_text = @Base_Text 
                   " + (languageId.HasValue ? "AND language_id = @Language_Id" : "") + ")";

            return await conn.ExecuteScalarAsync<bool>(sql, new { Base_Text = baseText, Language_Id = languageId });
        }

        public async Task<IEnumerable<Word>> GetAllWordsAsync()
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryAsync<Word>("SELECT * FROM words");
        }

        public async Task RemoveAsync(Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            const string sql = "DELETE FROM words WHERE id = @Id";
            await conn.ExecuteAsync(sql, new { Id = wordId });
        }

        public async Task AddWordAsync(Word word)
        {
            //if (await WordExistsAsync(word.Base_Text, word.Language_Id)) return;
            var sql = @"INSERT INTO words (id, base_text, language_id)
                    VALUES (@Id, @Base_Text, @Language_Id)";

            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(sql, word);
        }

        public async Task<Word?> GetWordById(Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT * FROM words WHERE id = @Word_Id";
           return await conn.QueryFirstOrDefaultAsync<Word>(sql, new {Word_Id = wordId} );
        }

        public async Task<int> GetTotalCountAsync()
        {
            using var conn = _factory.CreateConnection();
            const string sql = "SELECT COUNT(*) FROM words";
            return await conn.ExecuteScalarAsync<int>(sql);
        }

        public async Task<IDictionary<string, int>> GetCountByLanguageAsync()
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT l.name AS Name, COUNT(*) AS Count
                         FROM words w
                         JOIN languages l ON w.language_id = l.id
                         GROUP BY l.name
                         ORDER BY l.name";
            var rows = await conn.QueryAsync<(string Name, int Count)>(sql);
            return rows.ToDictionary(r => r.Name, r => r.Count);
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
