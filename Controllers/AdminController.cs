using Microsoft.AspNetCore.Mvc;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;
using TelegramWordBot.Models;

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

    public AdminController(
        WordImageRepository imageRepo,
        IImageService imageService,
        WordRepository wordRepo,
        TranslationRepository translationRepo,
        LanguageRepository languageRepo)
    {
        _imageRepo = imageRepo;
        _imageService = imageService;
        _wordRepo = wordRepo;
        _translationRepo = translationRepo;
        _languageRepo = languageRepo;
    }

    [HttpPost("images/download")]
    public async Task<IActionResult> DownloadImages()
    {
        var words = (await _imageRepo.GetWordsWithoutImagesAsync()).ToList();
        int success = 0;
        int failed = 0;
        foreach (var w in words)
        {
            var path = await _imageService.GetImagePathAsync(w);
            if (string.IsNullOrEmpty(path))
                failed++;
            else
                success++;
        }

        return Ok(new { Total = words.Count(), Success = success, Failed = failed });
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
