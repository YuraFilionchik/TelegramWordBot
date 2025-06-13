using TelegramWordBot;
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class DictionaryRepository
{
    private readonly IConnectionFactory _factory;

    public DictionaryRepository(IConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddDictionaryAsync(Dictionary dict)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"INSERT INTO dictionaries (id, user_id, name) VALUES (@Id, @User_Id, @Name)";
        await conn.ExecuteAsync(sql, dict);
    }

    public async Task<Dictionary> GetDefaultDictionary(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT id AS Id, user_id AS User_Id, name AS Name FROM dictionaries WHERE user_id = @User_Id AND name = 'default' LIMIT 1";
        var defaultDict = await conn.QueryFirstOrDefaultAsync<Dictionary>(sql, new { User_Id = userId });
        if (defaultDict == null)
        {
            defaultDict = new Dictionary
            {
                Id = Guid.NewGuid(),
                User_Id = userId,
                Name = "default"
            };
            await AddDictionaryAsync(defaultDict);
            return defaultDict;
        }
        return defaultDict;
    }

    public async Task<IEnumerable<Dictionary>> GetByUserAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT id AS Id, user_id AS User_Id, name AS Name FROM dictionaries WHERE user_id = @User_Id";
        return await conn.QueryAsync<Dictionary>(sql, new { User_Id = userId });
    }

    public async Task AddWordAsync(Guid dictionaryId, Guid wordId)
    {
        if (await WordExistsAsync(wordId, dictionaryId))
            return; // Word already exists in the dictionary

        using var conn = _factory.CreateConnection();
        const string sql = @"INSERT INTO dictionary_words (dictionary_id, word_id) VALUES (@Dictionary_Id, @Word_Id) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { Dictionary_Id = dictionaryId, Word_Id = wordId });
    }

    private async Task<bool> DictionaryExistsAsync(string dictionaryName, Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT EXISTS (SELECT 1 FROM dictionaries WHERE name = @Dictionary_Name AND user_id = @User_Id)";
        return await conn.ExecuteScalarAsync<bool>(sql, new { Dictionary_Name = dictionaryName, User_Id = userId });
    }

    private async Task<bool> WordExistsAsync(Guid wordId, Guid dictionaryId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"SELECT EXISTS (SELECT 1 FROM dictionary_words WHERE word_id = @Word_Id AND dictionary_id = @Dictionary_Id)";
        return await conn.ExecuteScalarAsync<bool>(sql, new { Word_Id = wordId, Dictionary_Id = dictionaryId });
    }
    public async Task AddWordAsync(string dictionaryName, Guid wordId, Guid userId)
    {
        if (dictionaryName == "default")
        { 
            var defaultDict = await GetDefaultDictionary(userId); 
            await AddWordAsync(defaultDict.Id, wordId);
            return;
        }
        
        if (!await DictionaryExistsAsync(dictionaryName, userId))
        {
            var newDict = new Dictionary
            {
                Id = Guid.NewGuid(),
                User_Id = userId,
                Name = dictionaryName
            };
            await AddDictionaryAsync(newDict);
            await AddWordAsync(newDict.Id, wordId); 
            return;
        }

        using var conn = _factory.CreateConnection();
        const string sql = @"INSERT INTO dictionary_words (dictionary_id, word_id) 
            SELECT id, @Word_Id FROM dictionaries WHERE name = @Dictionary_Name AND user_id = @User_Id 
            ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { Dictionary_Name = dictionaryName, User_Id = userId,  Word_Id = wordId });
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

    public async Task DeleteAsync(Guid dictionaryId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM dictionaries WHERE id = @Id";
        await conn.ExecuteAsync(sql, new { Id = dictionaryId });
    }

    public async Task DeleteByUserAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "DELETE FROM dictionaries WHERE user_id = @User_Id";
        await conn.ExecuteAsync(sql, new { User_Id = userId });
    }
}
