using Dapper; 
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories { 
    public class LanguageRepository { 
        private readonly DbConnectionFactory _factory;

    public LanguageRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Language>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Language>("SELECT * FROM languages ORDER BY name");
    }

    public async Task<Language?> GetByCodeAsync(string code)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Language>("SELECT * FROM languages WHERE code = @code", new { code });
    }

    public async Task<Language?> GetByNameAsync(string name)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Language>("SELECT * FROM languages WHERE name = @name", new { name });
    }

    public async Task AddAsync(Language language)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("INSERT INTO languages (code, name) VALUES (@Code, @Name)", language);
    }

    public async Task DeleteAsync(string code)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM languages WHERE code = @code", new { code });
    }
}

}

