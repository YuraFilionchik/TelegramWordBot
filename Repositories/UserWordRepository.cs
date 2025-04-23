using Dapper;

namespace TelegramWordBot.Repositories;

public class UserWordRepository
{
    private readonly DbConnectionFactory _factory;

    public UserWordRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> UserHasWordAsync(Guid userId, string baseText)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM user_words uw
                JOIN words w ON uw.word_id = w.id
                WHERE uw.user_id = @UserId AND LOWER(w.base_text) = LOWER(@BaseText)
            )";

        return await conn.ExecuteScalarAsync<bool>(sql, new { UserId = userId, BaseText = baseText });
    }

    public async Task AddUserWordAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "INSERT INTO user_words (user_id, word_id) VALUES (@UserId, @WordId) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { UserId = userId, WordId = wordId });
    }

    public async Task RemoveAllUserWords()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_words";
        await conn.ExecuteAsync(sql);
    }
}
