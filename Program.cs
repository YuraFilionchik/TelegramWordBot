using Telegram.Bot;
using TelegramWordBot;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? throw new InvalidOperationException("CONNECTION_STRING environment variable is not set.");
var tokenTG = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
                ?? throw new Exception("TELEGRAM_TOKEN is null");
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(tokenTG));

connectionString = DbConnectionFactory.ConvertDatabaseUrl(connectionString);
var dbFactory = new DbConnectionFactory(connectionString);
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
builder.Services.AddHttpClient();



builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
