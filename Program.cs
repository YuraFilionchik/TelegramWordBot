using TelegramWordBot;
using TelegramWordBot.Repositories;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? throw new InvalidOperationException("DATABASE_URL environment variable is not set.");

connectionString = DbConnectionFactory.ConvertDatabaseUrl(connectionString);

var dbFactory = new DbConnectionFactory(connectionString);
builder.Services.AddSingleton(dbFactory);
builder.Services.AddSingleton<WordRepository>();
builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<UserWordProgressRepository>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
