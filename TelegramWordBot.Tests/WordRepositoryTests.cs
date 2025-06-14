using Dapper;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Xunit;

public class WordRepositoryTests : IDisposable
{
static WordRepositoryTests() { SqlMapper.AddTypeHandler(new GuidTypeHandler()); }
    private readonly TestDbConnectionFactory _factory;
    private readonly WordRepository _repo;

    public WordRepositoryTests()
    {
        _factory = new TestDbConnectionFactory();
        using var conn = _factory.CreateConnection();
        conn.Execute("CREATE TABLE languages (id INTEGER PRIMARY KEY, code TEXT, name TEXT);");
        conn.Execute("CREATE TABLE words (id BLOB PRIMARY KEY, base_text TEXT NOT NULL, language_id INTEGER);");
        _repo = new WordRepository(_factory);
    }

    [Fact]
    public async Task AddAndGetWordAsync()
    {
        using (var conn = _factory.CreateConnection())
        {
            conn.Execute("INSERT INTO languages (id, code, name) VALUES (1, 'en', 'English');");
        }
        var word = new Word { Id = Guid.NewGuid(), Base_Text = "hello", Language_Id = 1 };
        await _repo.AddWordAsync(word);
        var loaded = await _repo.GetByTextAndLanguageAsync("hello", 1);
        Assert.NotNull(loaded);
        Assert.Equal(word.Id, loaded!.Id);
    }

    [Fact]
    public async Task CountByLanguage_ReturnsCorrectCounts()
    {
        using (var conn = _factory.CreateConnection())
        {
            conn.Execute("INSERT INTO languages (id, code, name) VALUES (1, 'en','English'), (2, 'ru','Russian');");
        }
        await _repo.AddWordAsync(new Word { Id = Guid.NewGuid(), Base_Text = "one", Language_Id = 1 });
        await _repo.AddWordAsync(new Word { Id = Guid.NewGuid(), Base_Text = "two", Language_Id = 1 });
        await _repo.AddWordAsync(new Word { Id = Guid.NewGuid(), Base_Text = "odin", Language_Id = 2 });

        var counts = await _repo.GetCountByLanguageAsync();

        Assert.Equal(2, counts["English"]);
        Assert.Equal(1, counts["Russian"]);
    }

    [Fact]
    public async Task WordExistsAsync_ReturnsExpected()
    {
        using (var conn = _factory.CreateConnection())
        {
            conn.Execute("INSERT INTO languages (id, code, name) VALUES (1, 'en','English');");
        }
        await _repo.AddWordAsync(new Word { Id = Guid.NewGuid(), Base_Text = "test", Language_Id = 1 });

        Assert.True(await _repo.WordExistsAsync("test", 1));
        Assert.False(await _repo.WordExistsAsync("other", 1));
    }

    public void Dispose() => _factory.Dispose();
}
