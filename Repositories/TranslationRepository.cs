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
            VALUES (@Id, @Word_Id, @Language_Id, @Text, @Examples)";
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
        var sql = "SELECT * FROM translations WHERE word_id = @Word_Id";
        return await conn.QueryAsync<Translation>(sql, new { Word_Id = wordId });
    }

    public async Task<Translation?> GetTranslationAsync(Guid wordId, int targetLangId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @Word_Id AND language_id = @Language_Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Translation>(sql, new { Word_Id = wordId, Language_Id = targetLangId });
    }

    public async Task<bool> ExistTranslate(Guid? wordId, int targetLangId)
    {
        if (wordId == null) return false;
        using var conn = _factory.CreateConnection();
        var sql = @"SELECT EXISTS (
                   SELECT 1 FROM translations
                   WHERE word_id = @Word_Id AND language_id = @Language_Id)";

        return await conn.ExecuteScalarAsync<bool>(sql, new { Word_Id = wordId, Language_Id = targetLangId });
    }
}
