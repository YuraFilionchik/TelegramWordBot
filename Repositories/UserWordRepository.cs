using Dapper;
using TelegramWordBot.Models;

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
                WHERE uw.user_id = @User_Id AND LOWER(w.base_text) = LOWER(@Base_Text)
            )";
        //here is the bug with same words in different langs
        return await conn.ExecuteScalarAsync<bool>(sql, new { User_Id = userId, Base_Text = baseText });
    }

    public async Task AddUserWordAsync(Guid userId, Guid wordId, Guid? translationId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        INSERT INTO user_words (user_id, word_id, translation_id)
        VALUES (@User_Id, @Word_Id, @Translation_Id)
        ON CONFLICT (user_id, word_id) DO UPDATE SET translation_id = EXCLUDED.translation_id
    ";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Word_Id = wordId, Translation_Id = translationId });
    }

    public async Task<bool> RemoveUserWordAsync(Guid userId, string word)
    {
        using var conn = _factory.CreateConnection();

        var sql = @"
        DELETE FROM user_words
        USING words
        WHERE user_words.word_id = words.id
          AND user_words.user_id = @User_Id
          AND LOWER(words.base_text) = LOWER(@Word);
    ";

        var affectedRows = await conn.ExecuteAsync(sql, new { User_Id = userId, Word = word });
        return affectedRows > 0;
    }

    public async Task<bool> RemoveUserWordAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        const string sql = @"DELETE FROM user_words WHERE user_id = @User_Id AND word_id = @Word_Id";
        var affectedRows = await conn.ExecuteAsync(sql, new { User_Id = userId, Word_Id = wordId });
        return affectedRows > 0;
    }


    public async Task<IEnumerable<Word>> GetWordsByUserId(Guid? userId)
    {
        if (userId == null) return new List<Word>();
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT 
            w.id AS Id,
            w.base_text AS Base_Text,
            w.language_id AS Language_Id
        FROM user_words uw
        JOIN words w ON uw.word_id = w.id
        WHERE uw.user_id = @User_Id
    ";
        return await conn.QueryAsync<Word>(sql, new { User_Id = userId });
    }

    public async Task<IEnumerable<Word>> GetWordsByUserId(Guid? userId, int? LangId)
    {
        if (userId == null || LangId == null) return new List<Word>();
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT 
            w.id AS Id,
            w.base_text AS Base_Text,
            w.language_id AS Language_Id
        FROM user_words uw
        JOIN words w ON uw.word_id = w.id
        WHERE uw.user_id = @User_Id AND w.language_id = @Lang_Id
    ";
        return await conn.QueryAsync<Word>(sql, new { User_Id = userId, Lang_Id = LangId });
    }

    public async Task<UserWord?> GetUserWordAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT user_id AS User_Id, word_id AS Word_Id, translation_id AS Translation_Id
        FROM user_words
        WHERE user_id = @User_Id AND word_id = @Word_Id
        LIMIT 1;
    ";
        return await conn.QueryFirstOrDefaultAsync<UserWord>(sql, new { User_Id = userId, Word_Id = wordId });

    }

   
    public async Task UpdateUserTranslationIdAsync(Guid userId, Guid wordId, Guid translationId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        INSERT INTO user_words (user_id, word_id, translation_id)
        VALUES (@User_Id, @Word_Id, @Translation_Id)
        ON CONFLICT (user_id, word_id)
        DO UPDATE SET translation_id = EXCLUDED.translation_id;
        ";

        await conn.ExecuteAsync(sql, new
        {
            User_Id = userId,
            Word_Id = wordId,
            Translation_Id = translationId
        });
    }


    public async Task RemoveAllUserWords()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_words";
        await conn.ExecuteAsync(sql);
    }

    public async Task RemoveAllUserWords(User user)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_words WHERE user_id = @User_Id";
        await conn.ExecuteAsync(sql, new { User_Id = user.Id });
    }

}
