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
using System.Transactions;

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


            botClient.SetMyCommands(new[]
            {
                new BotCommand { Command = "start", Description = "Начать работу" },
                new BotCommand { Command = "user", Description = "Информация о пользователе" },
                new BotCommand { Command = "addlanguage", Description = "Добавить язык для изучения" },
                new BotCommand { Command = "addword", Description = "Добавить новое слово" },
                new BotCommand { Command = "removeword", Description = "Удалить слово" },
                new BotCommand { Command = "clearalldata", Description = "Удалить ВСЕ ДАННЫЕ!" },
                new BotCommand { Command = "switchlanguage", Description = "Изменить изучаемый язык" },
                new BotCommand { Command = "mywords", Description = "Показать мои слова" }

            });

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
                            ProcessAddWord(user, text, currentLearnLanguage, nativeLanguage, ct);
                            return;
                        case "awaiting_language":
                            _userStates.Remove(UserTelegramId);
                            ProcessAddLanguage(user, text);
                            break;
                        default:
                            await _msg.SendErrorAsync(chatId, $"Неизвестное состояние state {state}", ct);
                            break;
                    }
                    return;
                    
                }

                if (string.IsNullOrWhiteSpace(user.Native_Language))
                {
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
                    case "/mylangs":
                        var myLangs = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        if (myLangs == null || myLangs.Count() == 0)
                        {
                           await _msg.SendErrorAsync(chatId, "Ты еще не добавил ни одного языка", ct);
                        }
                        else
                        {
                            var langsString = "Ты изучаешь:" + Environment.NewLine;
                            foreach (var l in myLangs)
                            {
                                langsString += l + Environment.NewLine;
                            }
                            await _msg.SendInfoAsync(chatId, langsString, ct);
                        }
                            break;
                    case "/clearalldata":
                        await _msg.SendSuccessAsync(chatId, "Удаление слов и настроек...", ct);
                        user.Current_Language = "";
                        await _userRepo.UpdateAsync(user);
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

                    case "/removeword":
                        var splits = lowerText.Split(' ');
                        if (splits.Length <= 1)
                        {
                            await _msg.SendInfoAsync(chatId, "example: /removeword 'word'", ct);
                        }
                        else
                        {
                            var wordText = splits[1].Trim();
                            var removed = await _userWordRepo.RemoveUserWordAsync(user.Id, wordText);
                           if (removed) await _msg.SendInfoAsync(chatId, $"Удалено слово '{wordText}'", ct);
                           else await _msg.SendInfoAsync(chatId, $"Не найдено '{wordText}'", ct);
                        }

                            break;

                    case "/mywords":
                        //getmy words
                        var langs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
                        if (langs == null || langs.Count() == 0)
                        {
                            await _msg.SendErrorAsync(chatId, "Ты еще не добавил ни одного языка", ct);
                        }
                        else
                        {
                            foreach(var lang in langs)
                            {
                                var msgStr = "<b>" + lang.Name + ": </b>" + Environment.NewLine;
                                var words = await _userWordRepo.GetWordsByUserId(user.Id, lang.Id);
                                if (words == null || words.Count() == 0)
                                {
                                    msgStr += "Нет слов";
                                }else
                                    foreach (var w in words)
                                    {
                                        var translation = await _translationRepo.GetTranslationAsync(w.Id, nativeLanguage.Id);
                                        msgStr += w.Base_Text + " - " + translation?.Text  + Environment.NewLine;
                                    }
                                await botClient.SendMessage(chatId, msgStr, parseMode: ParseMode.Html, cancellationToken: ct);
                            }
                        }
                        break;
                    case "switchlanguage":

                        break;
                    default: //поиск слова и вывод его карточки
                        if (user.Current_Language == null)
                        {
                            await _msg.SendErrorAsync(chatId, "Ты еще не добавил ни одного языка", ct);
                            return;
                        }
                        var word = _wordRepo.GetByTextAsync(lowerText);
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
        
        private async void ProcessAddWord(Models.User user, string text, Language currentLearnLanguage, Language nativeLanguage, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            if (user.Current_Language == null || user.Native_Language == null || currentLearnLanguage == null)
            {
                await _msg.SendErrorAsync(chatId, "Не настроены родной или изучаемый язык пользователя", ct);
                return;
            }
            
            var exists = await _userWordRepo.UserHasWordAsync(user.Id, text);
            if (exists)
            {
                await _msg.SendInfoAsync(chatId, $"\"{text}\" уже есть в твоём списке.", ct);
                return;
            }

            var newWord = await CreateWordWithTranslationAsync(user.Id, text, nativeLanguage, currentLearnLanguage);
           // await _userWordRepo.AddUserWordAsync(user.Id, newWord.Id);
            var translation = await _translationRepo.GetTranslationAsync(newWord.Id, nativeLanguage.Id) ?? throw new NullReferenceException("Can not get translation for word");
            await _msg.SendSuccessAsync(chatId, $"Добавлено {newWord.Base_Text}", ct);
            await _msg.SendWordCardAsync(chatId, newWord.Base_Text, translation.Text + "\n" + translation.Examples, null, ct);
            //TODO fix Examples
        }
        private async void ProcessAddNativeLanguage(Models.User user, string? text)
        {
            var langInfo = await _ai.GetLangName(text);
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
            var langInfo = await _ai.GetLangName(text);
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
            await _botClient.SendMessage(chatId, $"Теперь можете добавлять слова для изучения с помощью команды /addword или меню");
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

        private async Task<Word> CreateWordWithTranslationAsync(Guid userId, string inputText, Language nativeLang, Language targetLang)
        {
            try
            {
                var inputTextLanguage = await _ai.GetLangName(inputText);
                if (string.IsNullOrWhiteSpace(inputTextLanguage) || inputTextLanguage.ToLower() == "error")
                {
                    throw new Exception("Не удалось определить язык текста: " + inputText);
                }

                Language inputLanguage = await _languageRepo.GetByNameAsync(inputTextLanguage);
                if (inputLanguage == null) throw new Exception($"Не удалось найти язык {inputTextLanguage} в базе");

                Guid translationId;
                string translationText = "";
                //inputText на иностранном языке
                if (inputLanguage.Id == targetLang.Id)
                {
                    //ищем в базе слов на targetLang
                    var word = await _wordRepo.GetByTextAndLanguageAsync(inputText, targetLang.Id);
                    if (word != null)
                    {
                        // слово уже есть в targetLang, проверим перевод (nativeLang)
                        var genTrans = await _translationRepo.GetTranslationAsync(word.Id, nativeLang.Id);
                        if (genTrans != null)
                        {
                            translationId = genTrans.Id;
                            translationText = genTrans.Text;
                        }
                        else
                        {
                            // перевод в nativeLang отсутствует, добавляем AI-перевод
                            var aiTranslation = await _ai.TranslateWordAsync(inputText, targetLang.Name, nativeLang.Name);
                            translationText = aiTranslation.TranslatedText;
                            if (aiTranslation == null || !aiTranslation.IsSuccess() || string.IsNullOrEmpty(aiTranslation.TranslatedText))
                            {
                                throw new Exception("Ошибка получения перевода AI");
                            }

                            var newGenTrans = new Translation
                            {
                                Id = Guid.NewGuid(),
                                Word_Id = word.Id,
                                Language_Id = nativeLang.Id,
                                Text = translationText
                            };
                            await _translationRepo.AddTranslationAsync(newGenTrans);
                            translationId = newGenTrans.Id;
                        }
                        //есть и слово и перевод
                        await _userWordRepo.AddUserWordAsync(userId, word.Id);
                        return word;
                    }
                    else
                    {
                        //создаем новое слово  и перевод на родной язык
                        Word newWord = new()
                        {
                            Id = new Guid(),
                            Base_Text = inputText,
                            Language_Id = targetLang.Id
                        };

                        //переводим
                        var translation = await _ai.TranslateWordAsync(inputText, targetLang.Name, nativeLang.Name);
                        if (translation == null || !translation.IsSuccess() || string.IsNullOrEmpty(translation.TranslatedText))
                        {
                            throw new Exception("Ошибка получения перевода AI");
                        }

                        Translation wordTranslation = new Translation
                        {
                            Id = Guid.NewGuid(),
                            Word_Id = newWord.Id,
                            Language_Id = nativeLang.Id,
                            Text = translation.TranslatedText
                        };
                        await _wordRepo.AddWordAsync(newWord);
                        await _translationRepo.AddTranslationAsync(wordTranslation);
                        await _userWordRepo.AddUserWordAsync(userId, newWord.Id);
                        return newWord;
                    }
                }
                else////inputText на родном языке
                {
                    //ищем в переводах
                    var translates = await _translationRepo.FindWordByText(inputText);
                    if (translates != null && translates.Count() != 0)//что-то есть
                    {
                        var nativeTranslate = translates.First(x => x.Language_Id == nativeLang.Id);
                        if (nativeTranslate != null) //есть слово в списке переводов
                        {
                            var foreignWord = await _wordRepo.GetWordById(nativeTranslate.Word_Id);
                            if (foreignWord != null)
                            {
                                //есть перевод, есть само слово на TargetLang и они связаны
                                //по идее ничего не нужно делать, только добавить к пользователю
                                await _userWordRepo.AddUserWordAsync(userId, foreignWord.Id);
                                return foreignWord;
                            }
                            else //есть только перевод, но нет самого иностранного слова (по каким-либо причинам)
                            {
                                var translToForeign = await _ai.TranslateWordAsync(inputText, nativeLang.Code, targetLang.Code);
                                Word word = new()
                                {
                                    Id = nativeTranslate.Word_Id,
                                    Base_Text = translToForeign.TranslatedText ?? "no translation",
                                    Language_Id = targetLang.Id

                                };
                                await _wordRepo.AddWordAsync(word);
                                await _userWordRepo.AddUserWordAsync(userId, word.Id);
                                return word;
                            }
                        }

                    }
                    //нет слова в базе переводов, inputText на родном языке
                    //переводим на иностранный
                    var translation = await _ai.TranslateWordAsync(inputText, nativeLang.Name, targetLang.Name);
                    if (translation == null || !translation.IsSuccess() || string.IsNullOrEmpty(translation.TranslatedText))
                    {
                        throw new Exception("Ошибка получения перевода AI");
                    }

                    Word newWord = new Word
                    {
                        Id = new Guid(),
                        Base_Text = translation.TranslatedText,
                        Language_Id = targetLang.Id
                    };

                    Translation wordTranslation = new Translation
                    {
                        Id = Guid.NewGuid(),
                        Word_Id = newWord.Id,
                        Language_Id = nativeLang.Id,
                        Text = inputText
                    };
                    await _wordRepo.AddWordAsync(newWord);
                    await _translationRepo.AddTranslationAsync(wordTranslation);
                    await _userWordRepo.AddUserWordAsync(userId, newWord.Id);
                    return newWord;
                }
            }
            catch(Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        #region old method
        //private async Task<Word> CreateWordWithTranslationAsync(string inputText, Language nativeLang, Language targetLang)
        //{
        //    //looking for Word in Base
        //    var wordFromBase = await _wordRepo.GetByTextAsync(inputText);

        //    //существует ли слово и перевод к нему на targetLang 
        //    bool isExistTranslate = await _translationRepo.ExistTranslate(wordFromBase?.Id, targetLang.Id);
        //    if (!isExistTranslate)
        //    {
        //        //ввели иностранное и ищем перевод на родной язык
        //        isExistTranslate = await _translationRepo.ExistTranslate(wordFromBase?.Id, nativeLang.Id);
        //        if (isExistTranslate)//normal case
        //        {
        //            return wordFromBase;
        //        }
        //        else //no any translations
        //        {
        //            var translation = await _ai.TranslateWordAsync(inputText, nativeLang.Name, targetLang.Name);
        //            if (translation == null || string.IsNullOrEmpty(translation.TranslatedText))
        //            {
        //                throw new NullReferenceException(nameof(translation));
        //            }

        //            if (!translation.IsSuccess())
        //            {
        //                throw new Exception(translation.Error);
        //            }

        //            Word word;
        //            bool inversed = false;
        //            //inputText на иностранном, перевод на родной
        //            if (translation.LanguageName?.ToLowerInvariant() == nativeLang.Name.ToLowerInvariant())
        //            {
        //                //sourceLang = targetLang;
        //                //targetLang = nativeLang;
        //                inversed = true;
        //                if (wordFromBase == null)
        //                {
        //                    word = new Word
        //                    {
        //                        Id = Guid.NewGuid(),
        //                        Base_Text = inputText,
        //                        Language_Id = targetLang.Id
        //                    };
        //                    await _wordRepo.AddWordAsync(word);
        //                }
        //                else
        //                {
        //                    word = wordFromBase;
        //                }
        //            }
        //            else //inputText на родном, перевод на Ин.яз
        //            {
        //                word = new Word
        //                {
        //                    Id = Guid.NewGuid(),
        //                    Base_Text = translation.TranslatedText,
        //                    Language_Id = targetLang.Id
        //                };
        //                await _wordRepo.AddWordAsync(word);
        //            }

        //            await _translationRepo.AddTranslationAsync(new Translation
        //            {
        //                Id = Guid.NewGuid(),
        //                Word_Id = word.Id,
        //                Language_Id = nativeLang.Id,
        //                Text = inversed ? translation.TranslatedText : inputText,
        //                Examples = translation.GetExampleString()
        //            });

        //            return word;
        //        }
        //    }
        //    else //is exist word and translation (слово на родном - перевод на иностранный)
        //    {
        //        var translatedText = await _translationRepo.GetTranslationAsync(wordFromBase.Id, targetLang.Id);
        //        if (!(await _wordRepo.WordExistsAsync(translatedText.Text, targetLang.Id)))
        //        {
        //            Word word = new Word
        //            {
        //                Id = Guid.NewGuid(),
        //                Base_Text = translatedText.Text,
        //                Language_Id = targetLang.Id
        //            };
        //            await _wordRepo.AddWordAsync(word);
        //            return word;
        //        }
        //        else return await _wordRepo.GetByTextAsync(translatedText.Text);
        //    }

        //}

        #endregion

    }
}
