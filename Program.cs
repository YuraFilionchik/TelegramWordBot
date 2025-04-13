using TelegramWordBot;
using TelegramWordBot.Repositories;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("CONNECtiON_STRING")
    ?? throw new InvalidOperationException("CONNECtiON_STRING environment variable is not set.");

connectionString = DbConnectionFactory.ConvertDatabaseUrl(connectionString);

var dbFactory = new DbConnectionFactory(connectionString);
builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<WordRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<UserWordProgressRepository>();
builder.Services.AddSingleton<LanguageRepository>();
builder.Services.AddSingleton<UserWordRepository>();
builder.Services.AddHttpClient<IAIHelper, AIHelper>();
builder.Services.AddSingleton<TranslationRepository>();
builder.Services.AddSingleton<UserLanguageRepository>();



builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
