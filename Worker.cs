using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using TelegramWordBot.Repositories;
using TelegramWordBot.Models;
using System.Collections.Concurrent;
using Dapper;
using static System.Net.Mime.MediaTypeNames;

namespace TelegramWordBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly TelegramBotClient _botClient;
        private readonly WordRepository _wordRepo;
        private readonly UserRepository _userRepo;
        private readonly UserWordRepository _userWordRepo;
        private readonly UserWordProgressRepository _progressRepo;
        private readonly LanguageRepository _languageRepo;
        private readonly TranslationRepository _translationRepo;
        private readonly UserLanguageRepository _userLangRepository;
        private readonly IAIHelper _ai;

        private readonly Dictionary<long, string> _userStates = new();

        public Worker(
            ILogger<Worker> logger,
            WordRepository wordRepo,
            UserRepository userRepo,
            UserWordProgressRepository progressRepo,
            LanguageRepository languageRepo,
            UserWordRepository userWordRepo,
            IAIHelper aiHelper,
            TranslationRepository translationRepository,
            UserLanguageRepository userLanguageRepository)
        {
            _logger = logger;
            _wordRepo = wordRepo;
            _userRepo = userRepo;
            _progressRepo = progressRepo;
            _languageRepo = languageRepo;
            _userWordRepo = userWordRepo;
            _ai = aiHelper;
            _translationRepo = translationRepository;
            _userLangRepository = userLanguageRepository;

            var tokenTG = Environment.GetEnvironmentVariable("TELEGRAM_TOKEN")
                ?? throw new Exception("TELEGRAM_TOKEN is null");

            _botClient = new TelegramBotClient(tokenTG);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = { } },
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe();
            _logger.LogInformation($"Бот {me.Username} запущен");
            await Task.Delay(-1, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.Message is not { } message || message.Text is not { } text)
                return;
            //var me =await _botClient.GetMe();
            var chatId = message.Chat.Id;
            var UserTelegramId = message.From!.Id;
            var lowerText = text.Trim().ToLowerInvariant();
            var user = await _userRepo.GetByTelegramIdAsync(UserTelegramId);
            var isNewUser = await IsNewUser(user, message);
            var userLanguages = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
     
            if (_userStates.TryGetValue(UserTelegramId, out string state))
            {
                if (state == "awaiting_addword")
                {
                    _userStates.Remove(UserTelegramId);
                        var newWord = await CreateWordWithTranslationAsync(text, user.NativeLanguage, user.CurrentLanguage);
                        var exists = await _userWordRepo.UserHasWordAsync(user.Id, text);
                        if (exists)
                        {
                            await botClient.SendMessage(chatId, $"Слово \"{text}\" уже есть в твоём списке.",cancellationToken: ct);
                            return;
                        }
                        
                        await _wordRepo.AddWordAsync(newWord);
                        await _userWordRepo.AddUserWordAsync(user.Id, newWord.Id);
                        var translation = _translationRepo.GetTranslationTextAsync(newWord);
                        await botClient.SendMessage(chatId, $"Добавлено. {text} - {translation}", cancellationToken: ct);
                    return;
                }else if (state == "awaiting_language")
                {
                    var langInfo = await _ai.GetLangInfo(text);
                    if (langInfo.ToLower() == "error")
                    {
                        await botClient.SendMessage(chatId, "Error adding new language");
                        return;
                    }
                    var langToAdd = await _languageRepo.GetByNameAsync(langInfo);
                    await _userLangRepository.AddUserLanguageAsync(user.Id, langToAdd.Id);
                    await botClient.SendMessage(chatId, $"Язык \"{langToAdd.Name}\" добавлен.", cancellationToken: ct);
                    return;
                }
            }

            // Команды
            switch (lowerText.Split(' ')[0])
            {
                case "/start":
                    ProcessStartCommand(user, message, botClient);
                    break;

                case "/addword":
                    if (userLanguages.Count() == 0)
                    {
                        await botClient.SendMessage(chatId, "Не могу добавить слово. Сначала укажи какой язык хочешь изучать командой /addlanguage, например 'addlanguage English'");
                        return;
                    }
                    _userStates[UserTelegramId] = "awaiting_addword";
                    await botClient.SendMessage(chatId, "Введите слово и его перевод в формате: слово - перевод", cancellationToken: ct);
                    break;

                case "/learn":
                    await botClient.SendMessage(chatId, "Режим изучения слов скоро будет доступен. (в разработке)", cancellationToken: ct);
                    break;

                case "/config":
                    await botClient.SendMessage(chatId, "Пока что настройка языка не реализована. (в разработке)", cancellationToken: ct);
                    break;

                case "/addlanguage":
                    if (lowerText.Split( ).Length == 1)
                    {
                        _userStates[UserTelegramId] = "awaiting_language";
                        await botClient.SendMessage(chatId, "Введите название языка для изучения");
                        return;
                    }
                    
                    break;

                case "/removelanguage":
                    var partsRemove = text.Split(' ', 2);
                    if (partsRemove.Length < 2)
                    {
                        await botClient.SendMessage(chatId, "Формат: /removelanguage [код]", cancellationToken: ct);
                        break;
                    }
                    await _languageRepo.DeleteAsync(partsRemove[1]);
                    await botClient.SendMessage(chatId, $"Язык с кодом \"{partsRemove[1]}\" удалён.", cancellationToken: ct);
                    break;

                case "/listlanguages":
                    var allLangs = await _languageRepo.GetAllAsync();
                    var msg = allLangs.Any()
                        ? string.Join("\n", allLangs.Select(l => $"{l.Code} — {l.Name}"))
                        : "Список языков пуст.";
                    await botClient.SendMessage(chatId, msg, cancellationToken: ct);
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

        private async void ProcessStartCommand(Models.User user, Message message, ITelegramBotClient botClient)
        {
            var isNewUser = await IsNewUser(user, message);
            var chatId = message.Chat.Id;
            var userLanguages = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
            if (isNewUser) //first time
            {
                await botClient.SendMessage(chatId, "Привет! Я бот для изучения слов. Используй команды:\n/addword — добавить слово\n/learn — тренировка\n/config — настройки языка\n/addlanguage [code] [name] — добавить язык\n/removelanguage [code] — удалить язык\n/listlanguages — список языков");

            }
            else if (userLanguages.Count() == 0)
            {
                await botClient.SendMessage(chatId, "Ты еще не выбрал язык для изучения. Введи /addlanguage - название языка, который хочешь изучать.");
            }
            else
            {
                string startMsg;
                switch (user.NativeLanguage.ToLower())
                {
                    case "ru":
                        startMsg = "Твой родной язык - Русский\\n";
                        startMsg += "Используй команды:\\n/addword — добавить слово\\n/learn — тренировка\\n/config — настройки языка\\n/addlanguage [code] [name] — добавить язык\\n/removelanguage [code] — удалить язык\\n/listlanguages — список языков";
                        break;
                    case "en":
                        startMsg = "Your native language is english\\n";
                        startMsg = "Use commands:\\n/addword — add word\\n/learn — training\\n/config — language settings\\n/addlanguage [code] [name] — add language\\n/removelanguage [code] — remove language\\n/listlanguages ​​— list of languages";
                        break;
                    default:
                        startMsg = "Your native language is english\\n";
                        startMsg = "Use commands:\\n/addword — add word\\n/learn — training\\n/config — language settings\\n/addlanguage [code] [name] — add language\\n/removelanguage [code] — remove language\\n/listlanguages ​​— list of languages";

                        break;
                }
                await botClient.SendMessage(chatId, startMsg);
            }
        }
        private async Task<bool> IsNewUser(Models.User user, Message message)
        {
            if (user == null) //first time
            {
                if (message == null) throw new NullReferenceException(nameof(message));
                user = new Models.User
                {
                    Id = Guid.NewGuid(),
                    Telegram_Id = message.From.Id,
                    NativeLanguage = message.From.LanguageCode ?? "Ru"  //TODO request and changing lang
                };
                await _userRepo.AddAsync(user);
                return true;
            }
            return false;
        }

        private async Task<Word> CreateWordWithTranslationAsync(string baseText, string nativeLang, string targetLang)
        {
            var translationReq = await _ai.TranslateWordAsync(baseText, nativeLang, targetLang);
            if (translationReq == null || translationReq.Split('=').Length < 2)
            {
                throw new FormatException(nameof(translationReq));
            }
            var translate = translationReq.Split('=');
            var translatedText = translate[1];
            if (translate[0] == "error")
            {
                throw new Exception(translate[1]);
            }
            var sourceLang = nativeLang;
            if (translate[0].ToLowerInvariant() == nativeLang.ToLowerInvariant())
            {
                sourceLang = targetLang;
                targetLang = nativeLang;
            }
            var srcLangId = _languageRepo.GetByNameAsync(sourceLang).Id;
            var targetLangId = _languageRepo.GetByNameAsync(targetLang).Id;
            var word = new Word
            {
                Id = Guid.NewGuid(),
                BaseText = baseText,
                LanguageId = srcLangId
            };
            await _wordRepo.AddWordAsync(word);
            await _translationRepo.AddTranslationAsync(new Translation
            {
                Id = Guid.NewGuid(),
                WordId = word.Id,
                LanguageId = targetLangId,
                Text = translatedText
            });
            

            return word;
        }

    }
}
