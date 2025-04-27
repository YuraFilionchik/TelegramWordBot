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

    public async Task<Translation?> GetTranslationTextAsync(Guid wordId, int targetLangId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @WordId AND language_id = @LanguageId LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Translation>(sql, new { WordId = wordId, LanguageId = targetLangId });
    }

    public async Task<bool> ExistTranslate(Guid? wordId, int targetLangId)
    {
        if (wordId == null) return false;
        using var conn = _factory.CreateConnection();
        var sql = @"SELECT EXISTS (
                   SELECT 1 FROM translations
                   WHERE word_id = @WordId AND language_id = @LanguageId";

        return await conn.ExecuteScalarAsync<bool>(sql, new { WordId = wordId, LanguageId = targetLangId });
    }
}
