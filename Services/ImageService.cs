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

namespace TelegramWordBot.Services
{
    public interface IImageService
    {
        Task<string> FetchAndSaveAsync(Guid wordId, string imageUrl);
        Task<string?> FetchFromPixabayAsync(Guid wordId, string query);
        Task<string?> FetchFromFlickrAsync(Guid wordId, string query);
        Task<string> SaveUploadedAsync(Guid wordId, Stream fileStream, string fileName);
        Task DeleteAsync(Guid wordId);
    }

    public class ImageService : IImageService
    {
        private readonly IHostEnvironment _env;
        private readonly WordImageRepository _repo;
        private readonly HttpClient _http;
        private readonly string? _pixabayKey;
        private readonly string? _flickrKey;

        public ImageService(HttpClient http, IHostEnvironment env, WordImageRepository repo)
        {
            _http = http;
            _env = env;
            _repo = repo;
            _pixabayKey = Environment.GetEnvironmentVariable("PIXABAY_API_KEY");
            _flickrKey = Environment.GetEnvironmentVariable("FLICKR_API_KEY");
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

        public async Task<string?> FetchFromFlickrAsync(Guid wordId, string query)
        {
            if (string.IsNullOrEmpty(_flickrKey))
                throw new InvalidOperationException("FLICKR_API_KEY is not set.");

            var requestUrl = $"https://www.flickr.com/services/rest/?method=flickr.photos.search&api_key={_flickrKey}&text={Uri.EscapeDataString(query)}&per_page=1&sort=relevance&content_type=1&media=photos&safe_search=1&format=json&nojsoncallback=1";
            var response = await _http.GetFromJsonAsync<FlickrSearchResponse>(requestUrl);
            var photo = response?.Photos?.Photo?.FirstOrDefault();
            if (photo == null)
                return null;

            var imageUrl = $"https://live.staticflickr.com/{photo.Server}/{photo.Id}_{photo.Secret}_b.jpg";
            return await FetchAndSaveAsync(wordId, imageUrl);
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
