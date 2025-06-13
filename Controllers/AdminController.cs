using Microsoft.AspNetCore.Mvc;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;

namespace TelegramWordBot.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly WordImageRepository _imageRepo;
    private readonly IImageService _imageService;

    public AdminController(WordImageRepository imageRepo, IImageService imageService)
    {
        _imageRepo = imageRepo;
        _imageService = imageService;
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
}
