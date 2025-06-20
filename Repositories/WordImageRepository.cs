using TelegramWordBot;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories
{

    public class WordImageRepository
    {
        private readonly IConnectionFactory _factory;
        public WordImageRepository(IConnectionFactory factory) => _factory = factory;

        public async Task AddAsync(WordImage img)
        {
            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(@"
            INSERT INTO word_images (id, word_id, file_path)
            VALUES (@Id, @WordId, @FilePath)", img);
        }

        public async Task<WordImage?> GetByWordAsync(Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<WordImage>(@"
            SELECT * FROM word_images WHERE word_id = @WordId", new { WordId = wordId });
        }

        public async Task DeleteByWordAsync(Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(@"
            DELETE FROM word_images WHERE word_id = @WordId", new { WordId = wordId });
        }

        public async Task UpdateAsync(WordImage img)
        {
            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(@"
            UPDATE word_images SET file_path = @FilePath WHERE id = @Id", img);
        }

        public async Task<int> CountWithImageAsync()
        {
            using var conn = _factory.CreateConnection();
            const string sql = @"SELECT COUNT(*) FROM words w
                                    JOIN word_images i ON w.id = i.word_id";
            return await conn.ExecuteScalarAsync<int>(sql);
        }

        public async Task<int> CountWithoutImageAsync()
        {
            using var conn = _factory.CreateConnection();
            const string sql = @"SELECT COUNT(*) FROM words w
                                    LEFT JOIN word_images i ON w.id = i.word_id
                                    WHERE i.word_id IS NULL";
            return await conn.ExecuteScalarAsync<int>(sql);
        }

        public async Task<IEnumerable<Word>> GetWordsWithoutImagesAsync()
        {
            using var conn = _factory.CreateConnection();
            const string sql = @"SELECT w.id AS Id,
                                       w.base_text AS Base_Text,
                                       w.language_id AS Language_Id
                                FROM words w
                                LEFT JOIN word_images i ON w.id = i.word_id
                                WHERE i.word_id IS NULL";
            return await conn.QueryAsync<Word>(sql);
        }
    }
}
