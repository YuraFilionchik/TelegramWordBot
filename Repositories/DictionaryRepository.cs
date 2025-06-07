using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class DictionaryRepository
{
    private readonly DbConnectionFactory _factory;

    public DictionaryRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddAsync(Dictionary dict)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"INSERT INTO dictionaries (id, user_id, name) VALUES (@Id, @User_Id, @Name)";
        await conn.ExecuteAsync(sql, dict);
    }

    public async Task<IEnumerable<Dictionary>> GetByUserAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT id AS Id, user_id AS User_Id, name AS Name FROM dictionaries WHERE user_id = @User_Id";
        return await conn.QueryAsync<Dictionary>(sql, new { User_Id = userId });
    }

    public async Task AddWordAsync(Guid dictionaryId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"INSERT INTO dictionary_words (dictionary_id, word_id) VALUES (@Dictionary_Id, @Word_Id) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { Dictionary_Id = dictionaryId, Word_Id = wordId });
    }

    public async Task RemoveWordAsync(Guid dictionaryId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"DELETE FROM dictionary_words WHERE dictionary_id = @Dictionary_Id AND word_id = @Word_Id";
        await conn.ExecuteAsync(sql, new { Dictionary_Id = dictionaryId, Word_Id = wordId });
    }

    public async Task<IEnumerable<Word>> GetWordsAsync(Guid dictionaryId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT w.id AS Id, w.base_text AS Base_Text, w.language_id AS Language_Id FROM dictionary_words dw JOIN words w ON dw.word_id = w.id WHERE dw.dictionary_id = @Dictionary_Id";
        return await conn.QueryAsync<Word>(sql, new { Dictionary_Id = dictionaryId });
    }
}
