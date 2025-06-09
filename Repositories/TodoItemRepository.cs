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

    public async Task<IEnumerable<TodoItem>> GetAllAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        IEnumerable<TodoItem> items = await conn.QueryAsync<TodoItem>(
            "SELECT * FROM todo_items WHERE user_id = @User_Id ORDER BY is_complete ASC, created_at DESC",
            new { User_Id = userId });
        return items;
    }

    public async Task AddAsync(TodoItem item)
    {
        using var conn = _factory.CreateConnection();
        const string sql = "INSERT INTO todo_items (id, user_id, title, description, created_at, is_complete) VALUES (@Id, @User_Id, @Title, @Description, @Created_At, @Is_Complete)";
        await conn.ExecuteAsync(sql, item);
    }

    public async Task UpdateAsync(TodoItem item)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE todo_items SET 
            title = @Title, 
            description = @Description, 
            is_complete = @Is_Complete,
            created_at = @Created_At
          WHERE id = @Id",
            item);
    }

    public async Task<TodoItem> GetByIdAsync(Guid id)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstAsync<TodoItem>("SELECT * FROM todo_items WHERE id=@Id", new { Id = id });
    }
}
