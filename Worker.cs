using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramWordBot.Repositories;
using TelegramWordBot.Models;

namespace TelegramWordBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelegramBotClient _botClient;
        private readonly WordRepository _wordRepo;
        private readonly UserRepository _userRepo;
        private readonly UserWordProgressRepository _progressRepo;

        // Состояние пользователя (userId → ожидание ввода)
        private readonly Dictionary<long, string> _userStates = new();

        public Worker(
            ILogger<Worker> logger,
            WordRepository wordRepo,
            UserRepository userRepo,
            UserWordProgressRepository progressRepo)
        {
            _logger = logger;
            _wordRepo = wordRepo;
            _userRepo = userRepo;
            _progressRepo = progressRepo;

            var token = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
                ?? throw new Exception("TELEGRAM_TOKEN is null");

            _botClient = new TelegramBotClient(token);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe(cancellationToken: stoppingToken);
            _logger.LogInformation($"Бот {me.Username} запущен");
            await Task.Delay(-1, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message || message.Text is not { } text)
                return;

            var chatId = message.Chat.Id;
            var telegramId = message.From!.Id;
            var lowerText = text.Trim().ToLower();

            // Проверка пользовательского состояния
            if (_userStates.TryGetValue(telegramId, out string state))
            {
                if (state == "awaiting_addword")
                {
                    _userStates.Remove(telegramId);
                    if (text.Contains("-"))
                    {
                        var parts = text.Split('-', 2);
                        var word = parts[0].Trim();
                        var translation = parts[1].Trim();

                        var newWord = new Word
                        {
                            Id = Guid.NewGuid(),
                            BaseText = word,
                            LanguageId = 1 // заглушка — пока не реализовано конфигурирование языков
                        };
                        await _wordRepo.AddWordAsync(newWord);

                        await botClient.SendMessage(chatId, $"Слово \"{word}\" добавлено. Перевод: {translation}", cancellationToken: ct);
                    }
                    else
                    {
                        await botClient.SendMessage(chatId, "Формат неправильный. Введите в формате: слово - перевод", cancellationToken: ct);
                    }
                    return;
                }
            }

            // Команды
            switch (lowerText)
            {
                case "/start":
                    await botClient.SendMessage(chatId, "Привет! Я бот для изучения слов. Используй команды:\n/addword — добавить слово\n/learn — тренировка\n/config — настройки языка", cancellationToken: ct);
                    break;

                case "/addword":
                    _userStates[telegramId] = "awaiting_addword";
                    await botClient.SendMessage(chatId, "Введите слово и его перевод в формате: слово - перевод", cancellationToken: ct);
                    break;

                case "/learn":
                    await botClient.SendMessage(chatId, "Режим изучения слов скоро будет доступен. (в разработке)", cancellationToken: ct);
                    break;

                case "/config":
                    await botClient.SendMessage(chatId, "Пока что настройка языка не реализована. (в разработке)", cancellationToken: ct);
                    break;

                default:
                    await botClient.SendMessage(chatId, "Неизвестная команда. Используй /start для справки.", cancellationToken: ct);
                    break;
            }
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
