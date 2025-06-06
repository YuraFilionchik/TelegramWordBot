using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class TodoItemRepository
{
    private readonly DbConnectionFactory _factory;

    public TodoItemRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<TodoItem>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<TodoItem>("SELECT * FROM todo_items");
    }

    public async Task AddAsync(TodoItem item)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "INSERT INTO todo_items (id, title, description, created_at, is_complete) VALUES (@Id, @Title, @Description, @Created_At, @Is_Complete)";
        await conn.ExecuteAsync(sql, item);
    }
}
