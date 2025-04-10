using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserRepository
{
    private readonly DbConnectionFactory _factory;

    public UserRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE telegram_id = @telegramId", new { telegramId });
    }

    public async Task AddAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, telegram_id, native_language) VALUES (@Id, @TelegramId, @NativeLanguage)", user);
    }
}
