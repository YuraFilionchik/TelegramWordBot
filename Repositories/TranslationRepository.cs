using System.Transactions;
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class TranslationRepository
{
    private readonly DbConnectionFactory _factory;

    public TranslationRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddTranslationAsync(Translation translation)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            INSERT INTO translations (id, word_id, language_id, text, examples)
            VALUES (@Id, @WordId, @LanguageId, @Text, @Examples)";
        await conn.ExecuteAsync(sql, translation);
    }

    public async Task RemoveAllTranslations()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM translations";
        await conn.ExecuteAsync(sql);
    }

    public async Task<IEnumerable<Translation>> GetTranslationsForWordAsync(Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @WordId";
        return await conn.QueryAsync<Translation>(sql, new { WordId = wordId });
    }

    public async Task<Translation?> GetTranslationTextAsync(Word word)
    {
        var wordId = word.Id;
        var targetLanguageId = word.LanguageId;
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @WordId AND language_id = @LanguageId LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Translation>(sql, new { WordId = wordId, LanguageId = targetLanguageId });
    }
}
