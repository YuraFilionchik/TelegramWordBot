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
            "SELECT * FROM users WHERE telegram_id = @Telegram_Id", new { Telegram_Id = telegramId });
        return user;
    }

    public async Task AddAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, telegram_id, native_language, current_language, prefer_multiple_choice, first_name, last_name, is_premium, user_name, last_seen) " +
            "VALUES (@Id, @Telegram_Id, @Native_Language, @Current_Language, @Prefer_Multiple_Choice, @First_Name, @Last_Name, @Is_Premium, @User_Name, @Last_Seen)", user);
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET telegram_id = @Telegram_Id, native_language = @Native_Language, current_language = @Current_Language, prefer_multiple_choice = @Prefer_Multiple_Choice, " +
            "first_name = @First_Name, last_name = @Last_Name, is_premium = @Is_Premium, user_name = @User_Name, last_seen = @Last_Seen WHERE id = @Id",
            user);
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<User>("SELECT * FROM users");
    }

    public async Task DeleteAsync(Guid id)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM users WHERE id = @Id", new { Id = id });
    }
}
