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
using TelegramWordBot.Services;
using System.Runtime.CompilerServices;

namespace TelegramWordBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly ITelegramBotClient _botClient;
        private readonly WordRepository _wordRepo;
        private readonly UserRepository _userRepo;
        private readonly UserWordRepository _userWordRepo;
        private readonly UserWordProgressRepository _progressRepo;
        private readonly LanguageRepository _languageRepo;
        private readonly TranslationRepository _translationRepo;
        private readonly UserLanguageRepository _userLangRepository;
        private readonly IAIHelper _ai;
        private readonly TelegramMessageHelper _msg;

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
            UserLanguageRepository userLanguageRepository,
            TelegramMessageHelper msg,
            ITelegramBotClient botClient)
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
            _msg = msg;
            _botClient = botClient;



            //   _botClient = new TelegramBotClient(tokenTG);
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
                var chatId = message.Chat.Id;
            try
            {
                //var me =await _botClient.GetMe();
                var UserTelegramId = message.From!.Id;
                var lowerText = text.Trim().ToLowerInvariant();
                var user = await _userRepo.GetByTelegramIdAsync(UserTelegramId);
                var isNewUser = await IsNewUser(user, message);
                
                if (isNewUser) user = await _userRepo.GetByTelegramIdAsync(UserTelegramId);
                
                var userLanguages = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                var nativeLanguage = await _languageRepo.GetByNameAsync(user.Native_Language);
                Language? currentLearnLanguage =await _languageRepo.GetByNameAsync(user.Current_Language);
                
                    

                if (_userStates.TryGetValue(UserTelegramId, out string state))
                {
                    switch(state)
                    {
                        case "awaiting_nativelanguage":
                            _userStates.Remove(UserTelegramId);
                            ProcessAddNativeLanguage(user, text);
                            break;

                        case "awaiting_addword":
                            _userStates.Remove(UserTelegramId);
                            if (user.Current_Language == null || user.Native_Language == null || currentLearnLanguage == null)
                            {
                                await _msg.SendErrorAsync(chatId, "Не настроены родной или изучаемый язык пользователя", ct);
                                return;
                            }
                            var exists = await _userWordRepo.UserHasWordAsync(user.Id, text);
                            if (exists)
                            {
                                await botClient.SendMessage(chatId, $"Слово \"{text}\" уже есть в твоём списке.", cancellationToken: ct);
                                return;
                            }
                            var newWord = await CreateWordWithTranslationAsync(text, nativeLanguage, currentLearnLanguage);
                            Language targetLang = currentLearnLanguage; //by default - input in native lang
                            if (newWord.LanguageId == currentLearnLanguage.Id)
                            {
                                targetLang = nativeLanguage;
                            }
                            await _userWordRepo.AddUserWordAsync(user.Id, newWord.Id);
                            var translation = await _translationRepo.GetTranslationTextAsync(newWord.Id, targetLang.Id) ?? throw new NullReferenceException("Can not get translation for word");
                            await botClient.SendMessage(chatId, $"Добавлено. {text} - {translation.Text}", cancellationToken: ct);
                            await _msg.SendWordCardAsync(chatId, text, translation.Text + "\n" + translation.Examples, null, ct);
                            return;

                        case "awaiting_language":
                            _userStates.Remove(UserTelegramId);
                            ProcessAddLanguage(user, text);
                            break;
                    }
                    
                }

                if (string.IsNullOrWhiteSpace(user.Native_Language))
                {
                    //TODO request native lang
                    _userStates[UserTelegramId] = "awaiting_nativelanguage";
                    await botClient.SendMessage(chatId, "Введите ваш родной язык(Enter your native language):");
                    return;
                }

                if (string.IsNullOrWhiteSpace(user.Current_Language))
                {
                    _userStates[UserTelegramId] = "awaiting_language";
                    await botClient.SendMessage(chatId, "Какой язык хотите изучать:");
                    return;
                }

                // Команды
                switch (lowerText.Split(' ')[0])
                {
                    case "/start":
                        ProcessStartCommand(user, message);
                        break;

                    case "/addword":
                        if (userLanguages.Count() == 0)
                        {
                            await botClient.SendMessage(chatId, "Не могу добавить слово. Сначала укажи какой язык хочешь изучать командой /addlanguage, например 'addlanguage English'");
                            return;
                        }
                        _userStates[UserTelegramId] = "awaiting_addword";
                        await botClient.SendMessage(chatId, "Введите слово для запоминания:", cancellationToken: ct);
                        break;

                    case "/learn":
                        await botClient.SendMessage(chatId, "Режим изучения слов скоро будет доступен. (в разработке)", cancellationToken: ct);
                        break;

                    case "/config":
                        await botClient.SendMessage(chatId, "Пока что настройка языка не реализована. (в разработке)", cancellationToken: ct);
                        break;

                    case "/addlanguage":
                        if (lowerText.Split().Length == 1)
                        {
                            _userStates[UserTelegramId] = "awaiting_language";
                            await botClient.SendMessage(chatId, "Введите название языка для изучения:");
                            return;
                        }else
                        {
                            ProcessAddLanguage(user, text);
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

                    case "/clearalldata":
                        await _msg.SendSuccessAsync(chatId, "Удаление слов и настроек...", ct);
                        await _translationRepo.RemoveAllTranslations();
                        await _userLangRepository.RemoveAllUserLanguages();
                        await _userWordRepo.RemoveAllUserWords();
                        await _wordRepo.RemoveAllWords();
                        await _msg.SendSuccessAsync(chatId, "Готово", ct);
                        break;
                    case "/user":
                        var lng = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        string lngs = String.Concat(lng);
                            await botClient.SendMessage(chatId, $"{message.From.FirstName} \n" +
                                $"{message.From.Username} \n" +
                                $"Native: {user.Native_Language} \n" +
                                $"Current: {user.Current_Language} \n" +
                                $"All: {lngs}",parseMode: ParseMode.MarkdownV2, cancellationToken: ct); 
                            break;
                    default:
                        await botClient.SendMessage(chatId, "Неизвестная команда. Используй /start для справки.", cancellationToken: ct);
                        break;
                }
            }catch(Exception ex)
            { _msg.SendErrorAsync(chatId, ex.Message, ct); }

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
        
            private async void ProcessAddNativeLanguage(Models.User user, string? text)
        {
            var langInfo = await _ai.GetLangInfo(text);
            var chatId = user.Telegram_Id;
            if (langInfo.ToLower() == "error")
            {
                await _botClient.SendMessage(chatId, "Error adding new language");
                return;
            }
            var langToAdd = await _languageRepo.GetByNameAsync(langInfo);
            user.Native_Language = langToAdd.Name;
            await _userRepo.UpdateAsync(user);
            //TODO switch interface language
            await _botClient.SendMessage(chatId, $"Язык \"{langToAdd.Name}\" добавлен.");
            return;
        }


        private async void ProcessAddLanguage(Models.User user, string? text)
        {
            var langInfo = await _ai.GetLangInfo(text);
            var chatId = user.Telegram_Id;
            if (langInfo.ToLower() == "error")
            {
                await _botClient.SendMessage(chatId, "Error adding new language");
                return;
            }
            var langToAdd = await _languageRepo.GetByNameAsync(langInfo);
            await _userLangRepository.AddUserLanguageAsync(user.Id, langToAdd.Id);
            user.Current_Language = langToAdd.Name;
            await _userRepo.UpdateAsync(user);
            await _botClient.SendMessage(chatId, $"Язык \"{langToAdd.Name}\" добавлен.");
            return;
        }

        private async void ProcessStartCommand(Models.User user, Message message)
        {
            var isNewUser = await IsNewUser(user, message);
            var chatId = message.Chat.Id;
            var userLanguages = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
            if (isNewUser) //first time
            {
                await _botClient.SendMessage(chatId, "Привет! Я бот для изучения слов. Используй команды:  /addword — добавить слово \n /learn — тренировка \n /config — настройки языка \n /addlanguage [code] [name] — добавить язык \n /removelanguage [code] — удалить язык \n /listlanguages — список языков");

            }
            else if (userLanguages.Count() == 0)
            {
                await _botClient.SendMessage(chatId, "Ты еще не выбрал язык для изучения. Введи /addlanguage - название языка, который хочешь изучать.");
            }
            else
            {
                string startMsg;
                switch (user.Native_Language.ToLower())
                {
                    case "russian":
                        startMsg = "Твой родной язык - Русский\n";
                        startMsg += "Используй команды: \n /addword — добавить слово \n /learn — тренировка \n /config — настройки языка \n /addlanguage  — добавить язык \n /removelanguage [code] — удалить язык \n /listlanguages — список языков";
                        break;
                    case "english":
                        startMsg = "Your native language is english\n";
                        startMsg = "Use commands:\n /addword — add word \n /learn — training \n /config — language settings \n /addlanguage [code] [name] — add language \n /removelanguage [code] — remove language \n /listlanguages ​​— list of languages";
                        break;
                    default:
                        startMsg = "Your native language is unknown\n";
                        startMsg = "Use commands:\n /addword — add word \n /learn — training \n /config — language settings \n /addlanguage [code] [name] — add language \n /removelanguage [code] — remove language \n /listlanguages ​​— list of languages";

                        break;
                }
                await _botClient.SendMessage(chatId, startMsg, parseMode: ParseMode.Html);
            }
        }
        private async Task<bool> IsNewUser(Models.User user, Message message)
        {
            if (user == null) //first time
            {
                if (message == null) throw new NullReferenceException(nameof(message));
                var lang = await _languageRepo.GetByCodeAsync(message.From.LanguageCode);
                
                user = new Models.User
                {
                    Id = Guid.NewGuid(),
                    Telegram_Id = message.From.Id,
                    Native_Language = lang == null ? "": lang.Name //TODO request and changing lang of interface
                };
                await _userRepo.AddAsync(user);
                return true;
            }
            return false;
        }

        private async Task<Word> CreateWordWithTranslationAsync(string baseText, Language nativeLang, Language targetLang)
        {
            var sourceLang = nativeLang;

            //looking for Word in Base
            var wordFromBase = await _wordRepo.GetByTextAsync(baseText);
            bool isExistTranslate = await _translationRepo.ExistTranslate(wordFromBase?.Id, targetLang.Id);
            if (!isExistTranslate)
            {
                isExistTranslate = await _translationRepo.ExistTranslate(wordFromBase?.Id, nativeLang.Id);
                if (isExistTranslate)
                {
                    return wordFromBase;
                }
                else //no translation
                {
                    var translation = await _ai.TranslateWordAsync(baseText, nativeLang.Name, targetLang.Name);
                    if (translation == null)
                    {
                        throw new NullReferenceException(nameof(translation));
                    }

                    if (!translation.IsSuccess())
                    {
                        throw new Exception(translation.Error);
                    }

                    if (translation.LanguageName?.ToLowerInvariant() == sourceLang.Name.ToLowerInvariant())
                    {
                        sourceLang = targetLang;
                        targetLang = nativeLang;
                    }

                    Word word;
                    if (wordFromBase == null)
                    {
                        word = new Word
                        {
                            Id = Guid.NewGuid(),
                            BaseText = baseText,
                            LanguageId = sourceLang.Id
                        };
                        await _wordRepo.AddWordAsync(word);
                    }
                    else word = wordFromBase;
                    
                    await _translationRepo.AddTranslationAsync(new Translation
                    {
                        Id = Guid.NewGuid(),
                        WordId = word.Id,
                        LanguageId = targetLang.Id,
                        Text = translation.TranslatedText ?? "no translations",
                        Examples = translation.GetExampleString()
                    });

                    return word;
                }    
            }
            else //is exist word and translation
            {
                return wordFromBase;
            }              
            
        }

    }
}
