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

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> MarkComplete(Guid id)
    {
        var item = await _repo.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        item.Is_Complete = true;
        await _repo.UpdateAsync(item);

        return RedirectToAction(nameof(GetPretty));
    }


    [HttpGet("pretty")]
    public async Task<IActionResult> GetPretty()
    {
        var items = await _repo.GetAllAsync();
        var html = $@"
    <html>
    <head>
        <title>Список задач</title>
        <style>
            body {{ font-family: sans-serif; background: #fafbfc; color: #222; }}
            h1 {{ color: #4267b2; }}
            .item {{ border-radius:12px; margin:12px 0; padding:16px; background:#fff; box-shadow:0 1px 6px #0001; }}
            .done {{ text-decoration:line-through; color:#aaa; }}
            .created {{ font-size:0.9em; color:#aaa; }}
            .link-btn {{
                display: inline-block; margin-top: 10px; background: #4267b2; color: #fff; padding: 6px 14px;
                border-radius: 8px; text-decoration: none; font-size: 0.96em;
                transition: background 0.2s; 
            }}
            .link-btn:hover {{ background: #1a418e; }}
        </style>
    </head>
    <body>
        <h1>📝 Список задач</h1>
        <ul style='list-style:none; padding:0;'>
        {string.Join("\n", items.Select(x => $@"
            <li class='item{(x.Is_Complete ? " done" : "")}'>
                <b>{System.Net.WebUtility.HtmlEncode(x.Title)}</b><br>
                {System.Net.WebUtility.HtmlEncode(x.Description)}<br>
                <span class='created'>Создано: {x.Created_At:dd.MM.yyyy HH:mm}</span><br>
                {(!x.Is_Complete ? $"<form method='post' action='/todoitems/{x.Id}/complete' style='display:inline;'><button class='link-btn'>Выполнено</button></form>" : "<span style='color:#2e7d32;'>✔ Выполнено</span>")}
            </li>"))}
        </ul>
        <p>Всего задач: {items.Count()}</p>
    </body>
    </html>";
        return Content(html, "text/html; charset=utf-8");
    }

}
