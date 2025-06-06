using Microsoft.AspNetCore.Mvc;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;

namespace TelegramWordBot.Controllers;

[ApiController]
[Route("[controller]")]
public class TodoItemsController : ControllerBase
{
    private readonly TodoItemRepository _repo;

    public TodoItemsController(TodoItemRepository repo)
    {
        _repo = repo;
    }

    [HttpGet]
    public async Task<IEnumerable<TodoItem>> Get()
    {
        return await _repo.GetAllAsync();
    }

    [HttpPost]
    public async Task<ActionResult<TodoItem>> Post([FromBody] TodoItem item)
    {
        item.Id = Guid.NewGuid();
        item.Created_At = DateTime.UtcNow;
        await _repo.AddAsync(item);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
}
