using Microsoft.AspNetCore.Mvc;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;
using TelegramWordBot.Models;
using System.Linq;

namespace TelegramWordBot.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly WordImageRepository _imageRepo;
    private readonly IImageService _imageService;
    private readonly WordRepository _wordRepo;
    private readonly TranslationRepository _translationRepo;
    private readonly LanguageRepository _languageRepo;
    private readonly IAIHelper _ai;
    private static bool IsAdmin(long telegramId)
    {
        var adminId = Environment.GetEnvironmentVariable("ADMIN_ID");
        return !string.IsNullOrEmpty(adminId) && adminId == telegramId.ToString();
    }

    public AdminController(
        WordImageRepository imageRepo,
        IImageService imageService,
        WordRepository wordRepo,
        TranslationRepository translationRepo,
        LanguageRepository languageRepo,
        IAIHelper ai)
    {
        _imageRepo = imageRepo;
        _imageService = imageService;
        _wordRepo = wordRepo;
        _translationRepo = translationRepo;
        _languageRepo = languageRepo;
        _ai = ai;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] long telegramId)
    {
        if (!IsAdmin(telegramId))
            return Unauthorized();

        var total = await _wordRepo.GetTotalCountAsync();
        var byLang = await _wordRepo.GetCountByLanguageAsync();
        var withImg = await _imageRepo.CountWithImageAsync();
        var withoutImg = await _imageRepo.CountWithoutImageAsync();

        var html = $@"
<html>
<head>
    <title>Admin Dashboard</title>
    <style>
        body {{ font-family: sans-serif; background:#fafbfc; color:#222; }}
        h1 {{ color:#4267b2; }}
        .link-btn {{ display:inline-block; margin-top:8px; background:#4267b2; color:#fff; padding:6px 14px; border-radius:8px; text-decoration:none; font-size:0.96em; }}
        .link-btn:hover {{ background:#1a418e; }}
        form {{ margin-bottom:20px; }}
    </style>
</head>
<body>
    <h1>Admin Dashboard</h1>
    <p>Total words: {total}</p>
    <p>With images: {withImg}, without images: {withoutImg}</p>
    <ul>
        {string.Join("", byLang.Select(l => $"<li>{System.Net.WebUtility.HtmlEncode(l.Key)}: {l.Value}</li>"))}
    </ul>

    <form method='post' action='/admin/generate?telegramId={telegramId}'>
        <input type='text' name='theme' placeholder='Theme' required> 
        <input type='number' name='count' value='20' style='width:60px;'> 
        <input type='text' name='sourceLang' placeholder='Source lang' value='English'> 
        <input type='text' name='targetLang' placeholder='Target lang' value='Russian'>
        <button class='link-btn' type='submit'>Generate</button>
    </form>

    <form method='post' action='/admin/images/download?telegramId={telegramId}'>
        <button class='link-btn' type='submit'>Download Images</button>
    </form>

    <form method='post' action='/admin/images/clear?telegramId={telegramId}'>
        <button class='link-btn' type='submit'>Clear Local Images</button>
    </form>

    <form method='post' action='/admin/delete?telegramId={telegramId}'>
        <button class='link-btn' style='background:#c00;' type='submit'>Delete All Words</button>
    </form>
</body>
</html>";

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("images/download")]
    public async Task<IActionResult> DownloadImages([FromQuery] long telegramId)
    {
        if (!IsAdmin(telegramId))
            return Unauthorized();

        var words = (await _imageRepo.GetWordsWithoutImagesAsync()).ToList();
        foreach (var w in words)
        {
            await _imageService.GetImagePathAsync(w);
        }
        return RedirectToAction(nameof(Index), new { telegramId });
    }

    [HttpPost("images/clear")]
    public async Task<IActionResult> ClearImages([FromQuery] long telegramId)
    {
        if (!IsAdmin(telegramId))
            return Unauthorized();

        await _imageService.DeleteAllLocalImages();
        return RedirectToAction(nameof(Index), new { telegramId });
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromQuery] long telegramId, [FromForm] string theme, [FromForm] int count, [FromForm] string sourceLang, [FromForm] string targetLang)
    {
        if (!IsAdmin(telegramId))
            return Unauthorized();

        var result = await _ai.GetWordByTheme(theme, count, targetLang, sourceLang);
        await SaveGeneratedWordsAsync(result.Items);
        return RedirectToAction(nameof(Index), new { telegramId });
    }

    [HttpPost("delete")]
    public async Task<IActionResult> DeleteAll([FromQuery] long telegramId)
    {
        if (!IsAdmin(telegramId))
            return Unauthorized();

        await _imageService.DeleteAllLocalImages();
        await _translationRepo.RemoveAllTranslations();
        await _wordRepo.RemoveAllWords();
        return RedirectToAction(nameof(Index), new { telegramId });
    }

    private async Task SaveGeneratedWordsAsync(IEnumerable<TranslatedItem> items)
    {
        if (items == null || !items.Any())
            return;

        var first = items.First();
        var sourceLang = await _languageRepo.GetByNameAsync(first.OriginalLanguage);
        var targetLang = await _languageRepo.GetByNameAsync(first.TranslatedLanguage);

        if (sourceLang == null || targetLang == null)
            return;

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.OriginalText) || string.IsNullOrWhiteSpace(item.TranslatedText))
                continue;

            var word = new Word
            {
                Id = Guid.NewGuid(),
                Base_Text = item.OriginalText!,
                Language_Id = sourceLang.Id
            };

            var translation = new Translation
            {
                Id = Guid.NewGuid(),
                Word_Id = word.Id,
                Language_Id = targetLang.Id,
                Text = item.TranslatedText!,
                Examples = item.Example
            };

            await _wordRepo.AddWordAsync(word);
            await _translationRepo.AddTranslationAsync(translation);
        }
    }
}
