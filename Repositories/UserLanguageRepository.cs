using TelegramWordBot;
﻿using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserLanguageRepository
{
    private readonly IConnectionFactory _factory;

    public UserLanguageRepository(IConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "INSERT INTO user_languages (user_id, language_id) VALUES (@User_Id, @Language_Id) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Language_Id = languageId });
    }

    public async Task RemoveUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "DELETE FROM user_languages WHERE user_id = @User_Id AND language_id = @Language_Id";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Language_Id = languageId });
    }

    public async Task<IEnumerable<int>> GetUserLanguageIdsAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT language_id FROM user_languages WHERE user_id = @User_Id";
        return await conn.QueryAsync<int>(sql, new { User_Id = userId });
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
            WHERE ul.user_id = @User_Id";
            return await conn.QueryAsync<string>(sql, new { User_Id = userId });
        }catch
        {
            return new List<string>(); 
        }
    }

    public async Task<IEnumerable<Language>> GetUserLanguagesAsync(Guid userId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
    SELECT l.id AS Id, l.code AS Code, l.name AS Name
    FROM user_languages ul
    INNER JOIN languages l ON ul.language_id = l.id
    WHERE ul.user_id = @User_Id";
            return await conn.QueryAsync<Language>(sql, new { User_Id = userId });
        }
        catch
        {
            return new List<Language>();
        }
    }

    public async Task RemoveAllUserLanguages()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_languages";
        await conn.ExecuteAsync(sql);
    }

    public async Task RemoveAllUserLanguages(User user)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_languages WHERE user_id = @User_Id";
        await conn.ExecuteAsync(sql, new { User_Id = user.Id });
    }
}
