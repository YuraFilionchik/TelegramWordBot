using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelegramWordBot.Services
{
    public interface IImageService
    {
        Task<string> FetchAndSaveAsync(Guid wordId, string imageUrl);
        Task<string?> FetchFromPixabayAsync(Guid wordId, string query);
        Task<string?> FetchFromUnsplashAsync(Guid wordId, string query);
        Task<string?> FetchImageFromInternetAsync(Guid wordId, string query, string service);
        Task<string> SaveUploadedAsync(Guid wordId, Stream fileStream, string fileName);
        Task DeleteAsync(Guid wordId);
        Task<string?> GetImagePathAsync(Word word);
        Task DeleteAllLocalImages();
    }

    public class ImageService : IImageService
    {
        private readonly IHostEnvironment _env;
        private readonly WordImageRepository _repo;
        private readonly HttpClient _http;
        private readonly string? _pixabayKey;
        private readonly string? _unsplashKey;
        private readonly IAIHelper _ai;
        private readonly ILogger<ImageService> _logger;

        public ImageService(HttpClient http, IHostEnvironment env, WordImageRepository repo, IAIHelper ai, ILogger<ImageService> logger)
        {
            _http = http;
            _env = env;
            _repo = repo;
            _ai = ai;
            _logger = logger;
            _pixabayKey = Environment.GetEnvironmentVariable("PIXABAY_API_KEY");
            _unsplashKey = Environment.GetEnvironmentVariable("UNSPLASH_ACCESS_KEY");
        }

        public async Task<string> FetchAndSaveAsync(Guid wordId, string imageUrl)
        {
            var folder = Path.Combine(_env.ContentRootPath, "Images");
            Directory.CreateDirectory(folder);
            var ext = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = $"{wordId}{ext}";
            var path = Path.Combine(folder, fileName);

            var bytes = await _http.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(path, bytes);

            var existing = await _repo.GetByWordAsync(wordId);
            if (existing != null)
            {
                if (File.Exists(existing.FilePath)) File.Delete(existing.FilePath);
                existing.FilePath = path;
                await _repo.UpdateAsync(existing);
            }
            else
            {
                await _repo.AddAsync(new WordImage { Id = Guid.NewGuid(), WordId = wordId, FilePath = path });
            }

            return path;
        }

        public async Task<string?> FetchFromPixabayAsync(Guid wordId, string query)
        {
            if (string.IsNullOrEmpty(_pixabayKey))
                throw new InvalidOperationException("PIXABAY_API_KEY is not set.");

            var requestUrl = $"https://pixabay.com/api/?key={_pixabayKey}&q={Uri.EscapeDataString(query)}&image_type=photo&per_page=3&safesearch=true";
            var response = await _http.GetFromJsonAsync<PixabayResponse>(requestUrl);
            var imageUrl = response?.Hits?.FirstOrDefault()?.LargeImageURL;
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            return await FetchAndSaveAsync(wordId, imageUrl);
        }

        public async Task<string?> FetchFromUnsplashAsync(Guid wordId, string query)
        {
            if (string.IsNullOrEmpty(_unsplashKey))
                throw new InvalidOperationException("UNSPLASH_ACCESS_KEY is not set.");

            var requestUrl = $"https://api.unsplash.com/search/photos?query={Uri.EscapeDataString(query)}&client_id={_unsplashKey}&per_page=1";
            var response = await _http.GetFromJsonAsync<UnsplashSearchResponse>(requestUrl);
            var imageUrl = response?.Results?.FirstOrDefault()?.Urls?.Regular;
            if (string.IsNullOrEmpty(imageUrl))
                return null;

            return await FetchAndSaveAsync(wordId, imageUrl);
        }

        public Task<string?> FetchImageFromInternetAsync(Guid wordId, string query, string service)
        {
            switch (service.ToLowerInvariant())
            {
                case "pixabay":
                    return FetchFromPixabayAsync(wordId, query);
                case "unsplash":
                    return FetchFromUnsplashAsync(wordId, query);
                default:
                    throw new ArgumentException($"Unsupported image service: {service}");
            }
        }

        public async Task<string?> GetImagePathAsync(Word word)
        {
            var existing = await _repo.GetByWordAsync(word.Id);
            if (existing != null && File.Exists(existing.FilePath))
                return existing.FilePath;

            try
            {
                string searchQuery = await _ai.GetSearchStringForPicture(word.Base_Text);
                _logger.LogInformation("Fetching image for word {Word} with query '{Query}'", word.Base_Text, searchQuery);
                return await FetchImageFromInternetAsync(word.Id, searchQuery, "unsplash") ??
                       await FetchImageFromInternetAsync(word.Id, searchQuery, "pixabay");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении изображения для слова {WordId}", word.Id);
                return null;
            }
        }

        public async Task<string> SaveUploadedAsync(Guid wordId, Stream fileStream, string fileName)
        {
            var folder = Path.Combine(_env.ContentRootPath, "Images");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"{wordId}_{fileName}");
            await using var fs = File.Create(path);
            await fileStream.CopyToAsync(fs);

            // Если уже было изображение — удаляем старое записью и файлом
            var existing = await _repo.GetByWordAsync(wordId);
            if (existing != null)
            {
                if (File.Exists(existing.FilePath)) File.Delete(existing.FilePath);
                existing.FilePath = path;
                await _repo.UpdateAsync(existing);
            }
            else
            {
                await _repo.AddAsync(new WordImage { Id = Guid.NewGuid(), WordId = wordId, FilePath = path });
            }
            return path;
        }

        public Task DeleteAllLocalImages()
        {
            var folder = Path.Combine(_env.ContentRootPath, "Images");
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);

            Directory.CreateDirectory(folder);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid wordId)
        {
            var existing = await _repo.GetByWordAsync(wordId);
            if (existing != null)
            {
                if (File.Exists(existing.FilePath)) File.Delete(existing.FilePath);
                await _repo.DeleteByWordAsync(wordId);
            }
        }
    }
}
