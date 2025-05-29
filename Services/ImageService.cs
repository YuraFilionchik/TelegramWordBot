using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace TelegramWordBot.Services
{
    public interface IImageService
    {
        Task<string> FetchAndSaveAsync(Guid wordId, string imageUrl);
        Task<string> SaveUploadedAsync(Guid wordId, Stream fileStream, string fileName);
        Task DeleteAsync(Guid wordId);
    }

    public class ImageService : IImageService
    {
        private readonly IHostEnvironment _env;
        private readonly WordImageRepository _repo;

        public ImageService(IHostEnvironment env, WordImageRepository repo)
        {
            _env = env;
            _repo = repo;
        }

        public async Task<string> FetchAndSaveAsync(Guid wordId, string imageUrl)
        {
            // позже: получить байты через HttpClient, пока заглушка
            var folder = Path.Combine(_env.ContentRootPath, "Images");
            Directory.CreateDirectory(folder);
            var fileName = $"{wordId}{Path.GetExtension(imageUrl)}";
            var path = Path.Combine(folder, fileName);

            // TODO: загрузка из imageUrl → fileBytes
            // await File.WriteAllBytesAsync(path, fileBytes);

            var img = new WordImage { Id = Guid.NewGuid(), WordId = wordId, FilePath = path };
            await _repo.AddAsync(img);
            return path;
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
