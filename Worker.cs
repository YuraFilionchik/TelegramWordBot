using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;

namespace TelegramWordBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private TelegramBotClient _botClient;
        
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN");
            if (token == null)
            {
                throw new Exception("TELEGRAM_TOKEN is null");
            }
            _botClient = new TelegramBotClient(token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = { } },
            cancellationToken: stoppingToken
        );
            
            var me = await _botClient.GetMe();
            _logger.LogInformation($"Бот {me.Username} запущен.");

            await Task.Delay(-1, stoppingToken); // бесконечное ожидание
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message || message.Text is not { } text)
                return;

            Console.WriteLine($"Получено сообщение от {message.Chat.Username}: {text}");

            await botClient.SendMessage(
                chatId: message.Chat.Id,
                text: $"Ты написал: {text}",
                cancellationToken: ct
            );
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            var error = exception switch
            {
                ApiRequestException apiEx => $"Telegram API Error: {apiEx.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(error);
            return Task.CompletedTask;
        }
    }
}
