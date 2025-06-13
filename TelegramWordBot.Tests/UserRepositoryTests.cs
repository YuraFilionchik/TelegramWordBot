using Dapper;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Xunit;

public class UserRepositoryTests : IDisposable
{
static UserRepositoryTests() { SqlMapper.AddTypeHandler(new GuidTypeHandler()); }
    private readonly TestDbConnectionFactory _factory;
    private readonly UserRepository _repo;

    public UserRepositoryTests()
    {
        _factory = new TestDbConnectionFactory();
        using var conn = _factory.CreateConnection();
        conn.Execute(@"CREATE TABLE users (
            id BLOB PRIMARY KEY,
            telegram_id INTEGER NOT NULL,
            native_language TEXT,
            current_language TEXT,
            prefer_multiple_choice INTEGER,
            first_name TEXT,
            last_name TEXT,
            is_premium INTEGER,
            user_name TEXT,
            last_seen TEXT
        );");
        _repo = new UserRepository(_factory);
    }

    [Fact]
    public async Task AddAndGetUserAsync()
    {
        var user = new User { Id = Guid.NewGuid(), Telegram_Id = 111, Last_Seen = DateTime.UtcNow };
        await _repo.AddAsync(user);
        var loaded = await _repo.GetByTelegramIdAsync(111);
        Assert.NotNull(loaded);
        Assert.Equal(user.Id, loaded!.Id);
    }

    [Fact]
    public async Task UpdateUserAsync()
    {
        var user = new User { Id = Guid.NewGuid(), Telegram_Id = 222, Last_Seen = DateTime.UtcNow };
        await _repo.AddAsync(user);
        user.First_Name = "New";
        await _repo.UpdateAsync(user);
        var loaded = await _repo.GetByTelegramIdAsync(222);
        Assert.Equal("New", loaded!.First_Name);
    }

    public void Dispose() => _factory.Dispose();
}
