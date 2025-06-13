using Dapper;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Xunit;

public class AdminRepositoryTests : IDisposable
{
    static AdminRepositoryTests()
    {
        SqlMapper.AddTypeHandler(new GuidTypeHandler());
    }

    private readonly TestDbConnectionFactory _factory;
    private readonly WordRepository _wordRepo;
    private readonly TranslationRepository _translationRepo;
    private readonly UserWordRepository _userWordRepo;
    private readonly WordImageRepository _imageRepo;

    public AdminRepositoryTests()
    {
        var dbPath = Path.GetTempFileName();
        _factory = new TestDbConnectionFactory($"Data Source={dbPath};Foreign Keys=True");
        using var conn = _factory.CreateConnection();
        conn.Execute("CREATE TABLE languages (id INTEGER PRIMARY KEY, code TEXT, name TEXT);");
        conn.Execute("CREATE TABLE users (id BLOB PRIMARY KEY, telegram_id INTEGER);");
        conn.Execute("CREATE TABLE words (id BLOB PRIMARY KEY, base_text TEXT NOT NULL, language_id INTEGER);");
        conn.Execute(@"CREATE TABLE translations (
            id BLOB PRIMARY KEY,
            word_id BLOB REFERENCES words(id) ON DELETE CASCADE,
            language_id INTEGER,
            text TEXT NOT NULL,
            examples TEXT
        );");
        conn.Execute(@"CREATE TABLE user_words (user_id BLOB, word_id BLOB REFERENCES words(id) ON DELETE CASCADE, translation_id BLOB, PRIMARY KEY(user_id, word_id));");
        conn.Execute(@"CREATE TABLE word_images (id BLOB PRIMARY KEY, word_id BLOB REFERENCES words(id) ON DELETE CASCADE, file_path TEXT NOT NULL);");
        _wordRepo = new WordRepository(_factory);
        _translationRepo = new TranslationRepository(_factory);
        _userWordRepo = new UserWordRepository(_factory);
        _imageRepo = new WordImageRepository(_factory);
    }

    [Fact]
    public async Task DeletingWord_RemovesTranslationsAndUserLinks()
    {
        var userId = Guid.NewGuid();
        var wordId = Guid.NewGuid();
        var translationId = Guid.NewGuid();

        using (var conn = _factory.CreateConnection())
        {
            conn.Execute("INSERT INTO languages (id, code, name) VALUES (1,'en','English'),(2,'ru','Russian');");
            conn.Execute("INSERT INTO users (id, telegram_id) VALUES (@Id, 1);", new { Id = userId });
        }

        var word = new Word { Id = wordId, Base_Text = "test", Language_Id = 1 };
        await _wordRepo.AddWordAsync(word);

        var translation = new Translation { Id = translationId, Word_Id = wordId, Language_Id = 2, Text = "тест" };
        await _translationRepo.AddTranslationAsync(translation);
        await _userWordRepo.AddUserWordAsync(userId, wordId, translationId);

        await _wordRepo.RemoveAsync(wordId);

        using var verify = _factory.CreateConnection();
        var translations = verify.ExecuteScalar<int>("SELECT COUNT(*) FROM translations");
        var links = verify.ExecuteScalar<int>("SELECT COUNT(*) FROM user_words");
        var words = verify.ExecuteScalar<int>("SELECT COUNT(*) FROM words");

        Assert.Equal(0, words);
        Assert.Equal(0, translations);
        Assert.Equal(0, links);
    }

    [Fact]
    public async Task ImageStatistics_ReturnExpectedCounts()
    {
        using (var conn = _factory.CreateConnection())
        {
            conn.Execute("INSERT INTO languages (id, code, name) VALUES (1,'en','English');");
        }

        var word1 = new Word { Id = Guid.NewGuid(), Base_Text = "one", Language_Id = 1 };
        var word2 = new Word { Id = Guid.NewGuid(), Base_Text = "two", Language_Id = 1 };
        await _wordRepo.AddWordAsync(word1);
        await _wordRepo.AddWordAsync(word2);

        await _imageRepo.AddAsync(new WordImage { Id = Guid.NewGuid(), WordId = word1.Id, FilePath = "file1" });

        var withImg = await _imageRepo.CountWithImageAsync();
        var withoutImg = await _imageRepo.CountWithoutImageAsync();

        Assert.Equal(1, withImg);
        Assert.Equal(1, withoutImg);
    }

    public void Dispose() => _factory.Dispose();
}

