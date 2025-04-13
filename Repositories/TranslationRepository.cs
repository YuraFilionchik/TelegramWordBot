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
            INSERT INTO translations (id, word_id, language_id, text)
            VALUES (@Id, @WordId, @LanguageId, @Text)";
        await conn.ExecuteAsync(sql, translation);
    }

    public async Task<IEnumerable<Translation>> GetTranslationsForWordAsync(Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @WordId";
        return await conn.QueryAsync<Translation>(sql, new { WordId = wordId });
    }

    public async Task<string?> GetTranslationTextAsync(Word word)
    {
        var wordId = word.Id;
        var targetLanguageId = word.LanguageId;
        using var conn = _factory.CreateConnection();
        var sql = "SELECT text FROM translations WHERE word_id = @WordId AND language_id = @LanguageId LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<string>(sql, new { WordId = wordId, LanguageId = targetLanguageId });
    }
}
