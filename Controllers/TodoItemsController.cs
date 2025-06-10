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
    public async Task<IEnumerable<TodoItem>> Get([FromQuery] Guid userId)
    {
        return await _repo.GetAllAsync(userId);
    }

    [HttpPost]
    public async Task<ActionResult<TodoItem>> Post([FromQuery] Guid userId, [FromBody] TodoItem item)
    {
        item.Id = Guid.NewGuid();
        item.User_Id = userId;
        item.Created_At = DateTime.UtcNow;
        await _repo.AddAsync(item);
        return CreatedAtAction(nameof(Get), new { id = item.Id, userId = userId }, item);
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromQuery] Guid userId, [FromForm] string title, [FromForm] string description)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid(),
            User_Id = userId,
            Title = title,
            Description = description,
            Created_At = DateTime.UtcNow
        };
        await _repo.AddAsync(item);
        return RedirectToAction(nameof(GetPretty), new { userId });
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> MarkComplete(Guid id)
    {
        var item = await _repo.GetByIdAsync(id);
        if (item == null)
            return NotFound();

        item.Is_Complete = true;
        await _repo.UpdateAsync(item);

        return RedirectToAction(nameof(GetPretty), new { userId = item.User_Id });
    }


    [HttpGet("pretty")]
    public async Task<IActionResult> GetPretty([FromQuery] Guid userId)
    {
        var items = await _repo.GetAllAsync(userId);
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
        <script>
            function toggleCompleted() {{
                var show = document.getElementById('showCompleted').checked;
                document.querySelectorAll('.done').forEach(function(el) {{
                    el.style.display = show ? 'block' : 'none';
                }});
            }}
            window.onload = function() {{
                document.getElementById('showCompleted').checked = false;
                toggleCompleted();
            }}
        </script>
    </head>
    <body>
        <h1>📝 Список задач</h1>

        <form method='post' action='/todoitems/add?userId={userId}' style='margin-bottom:20px;'>
            <input type='text' name='title' placeholder='Заголовок' required><br>
            <textarea name='description' placeholder='Описание' rows='3'></textarea><br>
            <button class='link-btn' type='submit'>Добавить</button>
        </form>

        <label><input type='checkbox' id='showCompleted' onchange='toggleCompleted()'> Показывать выполненные</label>

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
