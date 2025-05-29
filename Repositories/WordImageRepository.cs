using System;
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
        private readonly DbConnectionFactory _factory;
        public WordImageRepository(DbConnectionFactory factory) => _factory = factory;

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
    }
}
