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
        User? user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE telegram_id = @telegramId", new { telegramId });
        user.Telegram_Id = telegramId;
        return user;
    }

    public async Task AddAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, telegram_id, native_language, current_language) VALUES (@Id, @Telegram_Id, @NativeLanguage, @CurrentLanguage)", user);
    }
}
