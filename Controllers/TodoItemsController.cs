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

    [HttpGet("pretty")]
    public async Task<IActionResult> GetPretty()
    {
        var items = await _repo.GetAllAsync();
        var html = $@"
    <html>
    <head>
        <title>Todo list</title>
        <style>
            body {{ font-family: sans-serif; background: #fafbfc; color: #222; }}
            h1 {{ color: #4267b2; }}
            .item {{ border-radius:12px; margin:12px 0; padding:16px; background:#fff; box-shadow:0 1px 6px #0001; }}
            .done {{ text-decoration:line-through; color:#aaa; }}
            .created {{ font-size:0.9em; color:#aaa; }}
        </style>
    </head>
    <body>
        <h1>📝 Tasks:</h1>
        <ul style='list-style:none; padding:0;'>
        {string.Join("\n", items.Select(x => $@"
            <li class='item{(x.Is_Complete ? " done" : "")}'>
                <b>{System.Net.WebUtility.HtmlEncode(x.Title)}</b><br>
                {System.Net.WebUtility.HtmlEncode(x.Description)}<br>
                <span class='created'>Created: {x.Created_At:dd.MM.yyyy HH:mm}</span>
            </li>"))}
        </ul>
        <p>Total tasks: {items.Count()}</p>
    </body>
    </html>";
        return Content(html, "text/html; charset=utf-8");
    }
}
