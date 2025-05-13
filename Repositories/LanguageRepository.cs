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

    public async Task<Language?> GetByCodeAsync(string? code)
    {
            if (code == null) return null;
            code = code.ToLower();
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Language>("SELECT * FROM languages WHERE code = @code", new { code });
    }

    public async Task<Language?> GetByNameAsync(string? name)
    {
            if (string.IsNullOrEmpty(name)) return null;

            // ������������ ��������: ������ ����� � ���������, ��������� � ��������
            string normalizedName = string.Create(name.Length, name, (chars, input) =>
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (i == 0)
                        ? char.ToUpperInvariant(input[i])
                        : char.ToLowerInvariant(input[i]);
                }
            });

            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Language>(
                "SELECT * FROM languages WHERE name = @normalizedName",
                new { normalizedName }
            );
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

