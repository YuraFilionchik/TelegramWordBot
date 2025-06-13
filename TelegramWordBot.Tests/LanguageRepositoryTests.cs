using Dapper;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Xunit;

public class LanguageRepositoryTests : IDisposable
{
    private readonly TestDbConnectionFactory _factory;
    private readonly LanguageRepository _repo;

    public LanguageRepositoryTests()
    {
        _factory = new TestDbConnectionFactory();
        using var conn = _factory.CreateConnection();
        conn.Execute("CREATE TABLE languages (id INTEGER PRIMARY KEY AUTOINCREMENT, code TEXT NOT NULL UNIQUE, name TEXT NOT NULL UNIQUE);");
        _repo = new LanguageRepository(_factory);
    }

    [Fact]
    public async Task AddAndGetByCodeAsync()
    {
        var lang = new Language { Code = "en", Name = "English" };
        await _repo.AddAsync(lang);

        var loaded = await _repo.GetByCodeAsync("EN");

        Assert.NotNull(loaded);
        Assert.Equal("en", loaded!.Code);
    }

    [Fact]
    public async Task GetByNameAsync_NormalizesName()
    {
        await _repo.AddAsync(new Language { Code = "ru", Name = "Russian" });
        var loaded = await _repo.GetByNameAsync("RUSSIAN");
        Assert.NotNull(loaded);
        Assert.Equal("Russian", loaded!.Name);
    }

    public void Dispose() => _factory.Dispose();
}
