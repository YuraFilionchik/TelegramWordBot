using Dapper;

namespace TelegramWordBot.Repositories;

public class UserLanguageRepository
{
    private readonly DbConnectionFactory _factory;

    public UserLanguageRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "INSERT INTO user_languages (user_id, language_id) VALUES (@UserId, @LanguageId) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { UserId = userId, LanguageId = languageId });
    }

    public async Task RemoveUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "DELETE FROM user_languages WHERE user_id = @UserId AND language_id = @LanguageId";
        await conn.ExecuteAsync(sql, new { UserId = userId, LanguageId = languageId });
    }

    public async Task<IEnumerable<int>> GetUserLanguageIdsAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT language_id FROM user_languages WHERE user_id = @UserId";
        return await conn.QueryAsync<int>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<string>> GetUserLanguageNamesAsync(Guid userId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
            SELECT l.name 
            FROM user_languages ul
            INNER JOIN languages l ON ul.language_id = l.id
            WHERE ul.user_id = @UserId";
            return await conn.QueryAsync<string>(sql, new { UserId = userId });
        }catch
        {
            return new List<string>(); 
        }
    }

    public async Task RemoveAllUserLanguages()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_languages";
        await conn.ExecuteAsync(sql);
    }
}
