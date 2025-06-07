using Telegram.Bot;
using TelegramWordBot;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;
using TelegramWordBot.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

var appUrl = Environment.GetEnvironmentVariable("APP_URL");
appUrl = FixUrlAndPort(appUrl);

if (!string.IsNullOrEmpty(appUrl))
{
    builder.WebHost.UseUrls(appUrl);
}

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");
var tokenTG = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
                ?? throw new Exception("TELEGRAM_TOKEN is null");
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(tokenTG));

connectionString = DbConnectionFactory.ConvertDatabaseUrl(connectionString);
var dbFactory = new DbConnectionFactory(connectionString);
await DatabaseInitializer.EnsureTablesAsync(dbFactory);
await DatabaseInitializer.EnsureLanguagesAsync(dbFactory);
builder.Services.AddSingleton<IConnectionFactory>(new DbConnectionFactory(connectionString));
builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(tokenTG));
builder.Services.AddSingleton<WordRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<UserWordProgressRepository>();
builder.Services.AddSingleton<LanguageRepository>();
builder.Services.AddSingleton<UserWordRepository>();
builder.Services.AddHttpClient<IAIHelper, AIHelper>();
builder.Services.AddSingleton<TranslationRepository>();
builder.Services.AddSingleton<UserLanguageRepository>();
builder.Services.AddSingleton<TelegramMessageHelper>();
builder.Services.AddSingleton<SpacedRepetitionService>();
builder.Services.AddSingleton<WordImageRepository>();
builder.Services.AddHttpClient<IImageService, ImageService>();
builder.Services.AddSingleton<TodoItemRepository>();

builder.Services.AddControllers();

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapControllers();

app.Run();

string FixUrlAndPort(string? url)
{
    if (string.IsNullOrEmpty(url)) return string.Empty;
    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        url = "http://" + url;
    
    var segments = url.Split(':');
    if (segments.Length == 2) //��� �����, ��������� 2311
    {
        url += ":2311";
    }
    
    return url.TrimEnd('/');
}