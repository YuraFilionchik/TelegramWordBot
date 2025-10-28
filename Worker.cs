using System.IO;
using System.Text;
using System.Linq;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramWordBot.Models;
using TelegramWordBot.Repositories;
using TelegramWordBot.Services;
using User = TelegramWordBot.Models.User;
using Microsoft.Extensions.Localization;
using System.Globalization; // Добавлено для CultureInfo

namespace TelegramWordBot
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;
        //private readonly IStringLocalizer<KeyboardFactory> _keyboardLocalizer; // Removed, as KeyboardFactory now handles its own localization
        private readonly KeyboardFactory _keyboardFactory; // Injected instance
        // private readonly IStringLocalizer<TelegramMessageHelper> _messageHelperLocalizer; // Already injected in TelegramMessageHelper
        private readonly ITelegramBotClient _botClient;
        private readonly WordRepository _wordRepo;
        private readonly UserRepository _userRepo;
        private readonly UserWordRepository _userWordRepo;
        private readonly UserWordProgressRepository _progressRepo;
        private readonly LanguageRepository _languageRepo;
        private readonly TranslationRepository _translationRepo;
        private readonly DictionaryRepository _dictionaryRepo;
        private readonly UserLanguageRepository _userLangRepository;
        private readonly TodoItemRepository _todoRepo;
        private readonly string _appUrl;
        private readonly IAIHelper _ai;
        private readonly TelegramMessageHelper _msg;
        private readonly Dictionary<long, string> _userStates = new();
        private readonly Dictionary<long, Language> _inputLanguage = new();
        private readonly Dictionary<long, TranslatedTextClass> _translationCandidates = new();
        private readonly Dictionary<long, List<int>> _selectedTranslations = new();
        private readonly Dictionary<long, List<int>> _selectedExamples = new();
        private readonly Dictionary<long, string> _pendingOriginalText = new();
        private readonly Dictionary<long, bool> _isNativeInput = new();
        private readonly SpacedRepetitionService _sr;
        private readonly IImageService _imageService;
        private readonly WordImageRepository _imageRepo;
        private readonly List<string> _supportedCultureStrings;
        private readonly TimeSpan _reminderCheckInterval = TimeSpan.FromMinutes(1);
        private readonly int[] _reminderHours = { 9, 12, 15, 19 };
        private DateTime _lastReminder = DateTime.MinValue;

        // Для режима редактирования:
        private readonly Dictionary<long, Guid> _pendingEditWordId = new();
        private readonly Dictionary<long, TranslatedTextClass> _editTranslationCandidates = new();
        private readonly Dictionary<long, List<int>> _selectedEditTranslations = new();
        private readonly Dictionary<long, List<int>> _selectedEditExamples = new();
        private readonly Dictionary<long, List<(Guid Id, string Display)>> _editListCache = new();
        private readonly Dictionary<long, int> _editListPage = new();
        private const int EditListPageSize = 30;
        private readonly Dictionary<long, Guid> _pendingDeleteWordsDict = new();
        private readonly Dictionary<long, Guid> _pendingDeleteDict = new();

        // TTS configuration stored here temporarily
        public static string TtsLanguage = "en-US";
        public static string TtsVoice = "en-US-Standard-B";
        public static double TtsSpeed = 1.0;

        public static TtsOptions GetDefaultTtsOptions() => new()
        {
            LanguageCode = TtsLanguage,
            VoiceName = TtsVoice,
            Speed = TtsSpeed
        };

        public Worker(
            ILogger<Worker> logger,
            WordRepository wordRepo,
            UserRepository userRepo,
            UserWordProgressRepository progressRepo,
            LanguageRepository languageRepo,
            UserWordRepository userWordRepo,
            IAIHelper aiHelper,
            TranslationRepository translationRepository,
            DictionaryRepository dictionaryRepository,
            UserLanguageRepository userLanguageRepository,
            TodoItemRepository todoItemRepository,
            TelegramMessageHelper msg,
            ITelegramBotClient botClient,
            SpacedRepetitionService sr,
            IImageService imageService,
            WordImageRepository imageRepo,
            IStringLocalizer<SharedResource> localizer,
            //IStringLocalizer<KeyboardFactory> keyboardLocalizer, // Removed
            KeyboardFactory keyboardFactory) // Added
        {
            _logger = logger;
            _localizer = localizer;
            //_keyboardLocalizer = keyboardLocalizer; // Removed
            _keyboardFactory = keyboardFactory; // Added
            _wordRepo = wordRepo;
            _userRepo = userRepo;
            _progressRepo = progressRepo;
            _languageRepo = languageRepo;
            _userWordRepo = userWordRepo;
            _ai = aiHelper;
            _translationRepo = translationRepository;
            _dictionaryRepo = dictionaryRepository;
            _userLangRepository = userLanguageRepository;
            _todoRepo = todoItemRepository;
            _msg = msg;
            _botClient = botClient;
            _sr = sr;
            _imageService = imageService;
            _imageRepo = imageRepo;
            _appUrl = Environment.GetEnvironmentVariable("APP_URL") ?? string.Empty;

            // Инициализируем список поддерживаемых культур из Program.cs (предполагается, что он там есть)
            // Это упрощенный вариант; в идеале это должно приходить из конфигурации или общего сервиса.
            _supportedCultureStrings = new List<string> { "ru-RU", "en-US", "fr-FR", "pl-PL", "de-DE", "zh-CN", "tr-TR", "et-EE" };
        }

        // Словарь для сопоставления имен языков из БД с кодами культур .NET
        private static readonly Dictionary<string, string> LanguageNameToCultureCodeMap = new()
        {
            // Основные языки из Program.cs
            { "Russian", "ru-RU" },
            { "English", "en-US" },
            { "French", "fr-FR" },
            { "Polish", "pl-PL" },
            { "German", "de-DE" },
            { "Chinese", "zh-CN" }, // Simplified Chinese
            { "Turkish", "tr-TR" },
            { "Estonian", "et-EE" },
            // Дополнительные языки из DbInitializer.cs (добавьте по мере необходимости)
            { "Hindi", "hi-IN" },
            { "Spanish", "es-ES" }, // Общий испанский, можно уточнить (es-MX, es-AR и т.д.)
            { "Arabic", "ar-SA" }, // Общий арабский, можно уточнить
            { "Bengali", "bn-BD" }, // или bn-IN
            { "Portuguese", "pt-PT" }, // или pt-BR
            { "Indonesian", "id-ID" },
            { "Urdu", "ur-PK" },
            { "Japanese", "ja-JP" },
            { "Swahili", "sw-KE" }, // Пример
            { "Marathi", "mr-IN" },
            { "Telugu", "te-IN" },
            { "Tamil", "ta-IN" },
            { "Vietnamese", "vi-VN" },
            { "Korean", "ko-KR" },
            { "Italian", "it-IT" },
            { "Ukrainian", "uk-UA" },
            { "Dutch", "nl-NL" },
            { "Gujarati", "gu-IN" },
            { "Persian", "fa-IR" },
            { "Malayalam", "ml-IN" },
            { "Thai", "th-TH" },
            { "Filipino", "fil-PH" },
            { "Burmese", "my-MM" },
            { "Esperanto", "eo" }, // Esperanto не имеет стандартного кода страны
            { "Swedish", "sv-SE" },
            { "Norwegian", "nb-NO" }, // Bokmål, основной
            { "Danish", "da-DK" },
            { "Finnish", "fi-FI" },
            { "Icelandic", "is-IS" },
            { "Greek", "el-GR" },
            { "Hungarian", "hu-HU" },
            { "Czech", "cs-CZ" },
            { "Slovak", "sk-SK" },
            { "Romanian", "ro-RO" },
            { "Bulgarian", "bg-BG" },
            { "Croatian", "hr-HR" },
            { "Serbian", "sr-RS" }, // или sr-Cyrl-RS / sr-Latn-RS
            { "Slovenian", "sl-SI" },
            { "Albanian", "sq-AL" },
            { "Latvian", "lv-LV" },
            { "Lithuanian", "lt-LT" },
            { "Irish", "ga-IE" },
            { "Maltese", "mt-MT" },
            { "Catalan", "ca-ES" },
            { "Basque", "eu-ES" },
            { "Welsh", "cy-GB" }
            // Добавьте остальные языки по аналогии
        };


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                cancellationToken: stoppingToken);

            _ = Task.Run(() => ReminderLoop(stoppingToken), stoppingToken);

            var me = await _botClient.GetMe();
            _logger.LogInformation($"Bot {me.Username} started");
            await Task.Delay(-1, stoppingToken);
        }

        private async Task<(bool handled, string newState)> HandleKeyboardCommandAsync(User user, string command, long chatId, CancellationToken ct)
        {
            // Локализуем команду для сравнения, если она пришла от клавиатуры
            // Используем _localizer для строк, которые могли быть определены в Worker.resx как общие команды,
            // и строки напрямую из _keyboardFactory._localizer для специфичных кнопок клавиатуры.
            // Однако, более чистым было бы, если бы KeyboardFactory возвращал не просто текст, а некий enum или константу команды.
            // Пока что для упрощения будем сравнивать с локализованными строками из KeyboardFactory.
            string commandKey = command; // По умолчанию ключ команды равен тексту команды

            // mainMenu
            if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(0).ElementAt(0).Text) commandKey = "my_words";
            else if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(0).ElementAt(1).Text) commandKey = "add_word";
            else if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(1).ElementAt(0).Text) commandKey = "statistics";
            else if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(1).ElementAt(1).Text) commandKey = "learn";
            else if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(2).ElementAt(0).Text) commandKey = "settings";
            else if (command == _keyboardFactory.GetMainMenu().Keyboard.ElementAt(2).ElementAt(1).Text) commandKey = "profile";
            // myWordsMenu
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(0).ElementAt(0).Text) commandKey = "show_all_words";
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(1).ElementAt(0).Text) commandKey = "dictionaries_by_topics";
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(2).ElementAt(0).Text) commandKey = "generate_new_words";
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(3).ElementAt(0).Text) commandKey = "edit_word";
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(4).ElementAt(0).Text) commandKey = "delete_words";
            else if (command == _keyboardFactory.GetMyWordsMenu().Keyboard.ElementAt(5).ElementAt(0).Text) commandKey = "back";


            switch (commandKey)
            {
                case "my_words":
                    await _keyboardFactory.ShowMyWordsMenuAsync(_botClient, chatId, ct);
                    return (true, string.Empty);

                case "show_all_words":
                    await ShowMyWords(chatId, user, ct);
                    return (true, string.Empty);

                case "dictionaries_by_topics":
                    await ShowDictionariesByTopics(chatId, ct);
                    return (true, string.Empty);

                case "edit_word":
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterWordForEditing"], ct);
                    return (true, "awaiting_editsearch");

                case "delete_words":
                    await ShowMyWordsForEdit(chatId, user, ct);
                    return (true, "awaiting_listdelete");

                case "back":
                    await _keyboardFactory.ShowMainMenuAsync(_botClient, chatId, ct);
                    return (true, string.Empty);

                case "add_word":
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterWordToAdd"], ct);
                    return (true, "awaiting_addword");

                case "learn":
                    await StartLearningAsync(user, ct);
                    return (true, string.Empty);

                case "settings":
                    await _keyboardFactory.ShowConfigMenuAsync(_botClient, chatId, user, ct);
                    return (true, string.Empty);

                case "statistics":
                    await _keyboardFactory.ShowStatisticsMenuAsync(_botClient, chatId, ct);
                    return (true, string.Empty);

                case "profile":
                    string url = _appUrl.StartsWith("http") ? _appUrl.Replace("http", "https") : "https://" + _appUrl;
                    await _keyboardFactory.ShowProfileMenuAsync(_botClient, chatId, user.Id, user.Telegram_Id, url, ct);
                    return (true, string.Empty);
                case "generate_new_words": // "Генерация новых слов"
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterThemeForWordGeneration"], ct);
                    return (true, "awaiting_generation_theme_input");

                default:
                    return (false, string.Empty);
            }
        }

        /// <summary>
        /// Обработчик callback-запросов: выбор переводов/примеров для добавления и редактирования.
        /// </summary>
        private async Task HandleCallbackAsync(ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
        {
            var data = callback.Data;
            var chatId = callback.Message!.Chat.Id;
            var userTelegramId = callback.From.Id;
            var user = await _userRepo.GetByTelegramIdAsync(userTelegramId);

            var parts = data.Split(':');
            var action = parts[0];
            switch (action)
            {
                case "learn": // learn:rem:wordId or learn:fail:wordId or learn:skip:wordId
                    var command = parts[1];
                    var wordId = Guid.Parse(parts[2]);
                    if (command == "skip")
                    {
                        await MarkWordAsKnownAsync(user, wordId, ct);
                    }
                    else
                    {
                        var success = command == "rem";
                        await UpdateLearningProgressAsync(user, wordId, success, ct);
                    }
                    await SendNextLearningWordAsync(user, chatId, ct);
                    break;
                case "delete":
                    var wordText = parts[1];
                    var removed = await _userWordRepo.RemoveUserWordAsync(user.Id, wordText);
                    if (removed)
                        await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordDeleted", wordText], ct);
                    else
                        await _msg.SendErrorAsync(chatId, _localizer["Worker.WordNotFound", wordText], ct);
                    break;
                case "repeat":
                    var repeatText = parts[1];
                    var w = await _wordRepo.GetByTextAsync(repeatText);
                    if (w != null)
                    {
                        var native = await _languageRepo.GetByNameAsync(user.Native_Language);
                        var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                        var imgPath = await _imageService.GetImagePathAsync(w);
                        var lang = await _languageRepo.GetByIdAsync(w.Language_Id);
                        await _msg.SendWordCardAsync(chatId, w.Base_Text, tr?.Text ?? string.Empty, tr?.Examples, imgPath, lang?.Name, ct);
                    }
                    break;
                case "favorite":
                    var favText = parts[1];
                    await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordAddedToFavorites", favText], ct);
                    break;
                case "edit":
                    var editId = Guid.Parse(parts[1]);
                    await ProcessEditWord(user, editId, ct);
                    break;
                case "set_native":
                    _userStates[userTelegramId] = "awaiting_nativelanguage";
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterNativeLanguage"], ct);
                    break;
                case "switch_language":
                    await HandleSwitchLanguageCommandAsync(user, chatId, ct);
                    break;
                case "add_foreign":
                    _userStates[userTelegramId] = "awaiting_language";
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterLanguageToLearn"], ct);
                    break;
                case "remove_foreign":
                    var langs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
                    if (!langs.Any())
                        await _msg.SendErrorAsync(chatId, _localizer["Worker.NoAddedLanguages"], ct);
                    else
                    {
                        var list = string.Join("\n", langs.Select(l => $"{l.Code} – {l.Name}"));
                        _userStates[userTelegramId] = "awaiting_remove_foreign";
                        await _msg.SendInfoAsync(chatId, _localizer["Worker.YourLanguages", list], ct);
                    }
                    break;
                case "switch_lang":
                    await ProcessSwitchLanguage(callback, chatId, user, parts, ct);
                    break;
                case "startedit":
                    var sid = Guid.Parse(parts[1]);
                    await ProcessEditWord(user, sid, ct);
                    break;
                case "dlistprev":
                case "dlistnext":
                    var newPage = int.Parse(parts[1]);
                    await SendEditListPage(chatId, newPage, ct);
                    await _botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                    break;
                case "prev":
                case "next":
                    await HandleSliderNavigationAsync(callback, user, parts, ct);
                    break;
                case "stat_today":
                    await ShowTodayStatistics(user, chatId, ct);
                    break;
                case "stat_total":
                    await ShowStatisticsAsync(user, chatId, ct);
                    break;
                case "stat_languages":
                    await ShowStatisticsByLanguages(user, chatId, ct);
                    break;
                case "profile_info":
                    await ShowProfileInfo(user, chatId, ct);
                    break;
                case "reset_profile_stats":
                    if (parts.Length > 1 && parts[1] == "confirm")
                    {
                        await ResetProfileStatistics(user, chatId, ct);
                    }
                    else
                    {
                        await _msg.SendConfirmationDialog(chatId,
                            _localizer["Worker.ResetStatisticsConfirmation"],
                            "reset_profile_stats:confirm",
                            _localizer["Worker.ButtonCancel"],
                            ct);
                    }
                    break;
                case "edit_dict":
                    await EditDictionary(parts[1], chatId, ct);
                    break;
                case "create_dict":
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterNewDictionaryName"], ct);
                    _userStates[userTelegramId] = "awaiting_create_dict_name";
                    return;
                case "reset_dict":
                    await ResetDictionaryProgress(parts[1], chatId, ct);
                    break;
                case "delete_dict":
                    await DeleteDictionary(parts[1], chatId, ct);
                    break;
                case "delete_dict_full":                    
                    await DeleteDictionaryFull(parts[1], chatId, ct);
                    break;
                case "show_dict":
                    if (Guid.TryParse(parts[1], out var sd))
                        await ShowDictionary(sd, chatId, ct);
                    break;
                case "delete_words":
                    if (Guid.TryParse(parts[1], out var dw))
                        await ShowDictionaryWordsForEdit(chatId, dw, user, ct);
                    break;
                case "cancel":
                    await _botClient.AnswerCallbackQuery(callback.Id, cancellationToken: ct);
                    break;
                case "fill_dict":
                    if (parts[1] == _localizer["Worker.ButtonCancel"])
                    {
                        await _msg.SendInfoAsync(chatId, _localizer["Worker.AutoFillDictionaryCancelled"], ct);
                        return;
                    }else if (!string.IsNullOrEmpty(parts[1]))
                    {
                        Dictionary dictionary;
                        TranslatedTextClass newWords;
                        if (_translationCandidates.ContainsKey(chatId))
                        {
                            newWords = _translationCandidates[chatId];
                            _translationCandidates.Remove(chatId);
                            dictionary = new Dictionary
                            {
                                Id = Guid.NewGuid(),
                                User_Id = user.Id,
                                Name = parts[1]
                            };
                            _dictionaryRepo.AddDictionaryAsync(dictionary);
                        }
                        else
                        {
                            dictionary = (await _dictionaryRepo.GetByUserAsync(user.Id)).First(x => x.Name == parts[1]);
                            newWords = await _ai.GetWordByTheme(parts[1], 20, user.Native_Language, user.Current_Language);
                        }

                        if (newWords == null || !newWords.Items.Any())
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.FailedToGetWordsForTheme"], ct);
                            return;
                        }
                        if (await SaveNewWords(user, newWords.Items, parts[1], ct))
                        {
                            await _msg.SendSuccessAsync(chatId, _localizer["Worker.DictionaryAutoFillSuccess", parts[1]], ct);
                            await ShowDictionary(dictionary.Id, chatId, ct);
                        }
                        else
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.ErrorSavingWords"], ct);
                        }
                    }
                    break;
                case "help_info":
                    await ShowHelpInformation(chatId, ct);
                    break;
                case "toggle_reminders":
                    user.Receive_Reminders = !user.Receive_Reminders;
                    await _userRepo.UpdateAsync(user);
                    var key = user.Receive_Reminders ? "Worker.RemindersEnabled" : "Worker.RemindersDisabled";
                    await _msg.SendSuccessAsync(chatId, _localizer[key], ct);
                    break;
                case "config_learn":
                    switch (parts[1])
                    {
                        case "main":
                            await _keyboardFactory.ShowLearnConfig(bot, chatId, user, ct);
                            return;
                        case "binary":
                            if (user.Prefer_Multiple_Choice)
                            {
                                user.Prefer_Multiple_Choice = false;
                                await _userRepo.UpdateAsync(user);
                                await _msg.SendSuccessAsync(chatId, _localizer["Worker.LearningModeChangedToBinary"], ct);
                            }
                            else
                            {
                                await _msg.SendErrorAsync(chatId, _localizer["Worker.AlreadyUsingBinaryLearningMode"], ct);
                            }
                            return;
                        case "multiple":
                            if (!user.Prefer_Multiple_Choice)
                            {
                                user.Prefer_Multiple_Choice = true;
                                await _userRepo.UpdateAsync(user);
                                await _msg.SendSuccessAsync(chatId, _localizer["Worker.LearningModeChangedToMultipleChoice"], ct);
                            }
                            else
                            {
                                await _msg.SendErrorAsync(chatId, _localizer["Worker.AlreadyUsingMultipleChoiceLearningMode"], ct);
                            }
                            return;
                    }
                    break;
            }



            // --- Добавление: переводы ---
            if (data.StartsWith("selectTrans"))
            {
                if (data == "selectTransDone")
                {
                    await ShowExampleOptions(chatId, _translationCandidates[chatId], ct);
                }
                else
                {
                    int idx = int.Parse(data.Split(':')[1]);
                    var sel = _selectedTranslations[chatId];
                    if (sel.Contains(idx)) sel.Remove(idx); else sel.Add(idx);
                    await ShowTranslationOptions(chatId, _translationCandidates[chatId], ct);
                }
                await bot.AnswerCallbackQuery(callback.Id);
                return;
            }

            // --- Добавление: примеры ---
            if (data.StartsWith("selectEx"))
            {
                if (data == "selectExDone")
                {
                    if (_isNativeInput[chatId])
                        await FinalizeAddWord(user!, null, ct);
                    else
                        await FinalizeAddWord(user!, _inputLanguage[chatId], ct);
                }
                else
                {
                    int idx = int.Parse(data.Split(':')[1]);
                    var sel = _selectedExamples[chatId];
                    if (sel.Contains(idx)) sel.Remove(idx); else sel.Add(idx);
                    await _botClient.DeleteMessage(callback.From.Id, callback.Message.Id);
                    await ShowExampleOptions(chatId, _translationCandidates[chatId], ct);
                }
                await bot.AnswerCallbackQuery(callback.Id);
                return;
            }

            // --- Редактирование: переводы ---
            if (data.StartsWith("editSelectTrans"))
            {
                if (data == "editSelectTransDone")
                {
                    await ShowEditExampleOptions(chatId, _editTranslationCandidates[chatId], ct);
                }
                else
                {
                    int idx = int.Parse(data.Split(':')[1]);
                    var sel = _selectedEditTranslations[chatId];
                    if (sel.Contains(idx)) sel.Remove(idx); else sel.Add(idx);
                    await ShowEditTranslationOptions(chatId, _editTranslationCandidates[chatId], ct);
                }
                await bot.AnswerCallbackQuery(callback.Id);
                return;
            }

            // --- Редактирование: примеры ---
            if (data.StartsWith("editSelectEx"))
            {
                if (data == "editSelectExDone")
                {
                    await FinalizeEditWord(user!, ct);
                }
                else
                {
                    int idx = int.Parse(data.Split(':')[1]);
                    var sel = _selectedEditExamples[chatId];
                    if (sel.Contains(idx)) sel.Remove(idx); else sel.Add(idx);
                    await ShowEditExampleOptions(chatId, _editTranslationCandidates[chatId], ct);
                }
                await bot.AnswerCallbackQuery(callback.Id);
                return;
            }

            // --- Режим обучения: множественный выбор ---
            if (data.StartsWith("mc:"))
            {
                // mc:correct:{wordId} или mc:wrong:{wordId} или mc:skip:{wordId}
                var sub = parts[1];
                var wordId = Guid.Parse(parts[2]);
                if (sub == "skip")
                {
                    await MarkWordAsKnownAsync(user, wordId, ct);
                }
                else
                {
                    var success = sub == "correct";
                    // Обновляем прогресс (SM-2) точно так же, как в бинарном режиме
                    await UpdateLearningProgressAsync(user, wordId, success, ct);
                }
                await Task.Delay(1000, ct); // Задержка перед отправкой следующего слова
                await SendNextLearningWordAsync(user, chatId, ct);
                return;
            }

            await bot.AnswerCallbackQuery(callback.Id);
        }

        private async Task DeleteDictionaryFull(string id, long chatId, CancellationToken ct)
        {
            if (id.StartsWith("confirm_"))
            {
                var strId = id.Substring("confirm_".Length);
                if (!Guid.TryParse(strId, out var dId)) return;
                await PerformDictionaryDeletion(dId, chatId, true, ct);
                return;
            }

            if (!Guid.TryParse(id, out var dictId))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.InvalidIdentifier"], ct);
                return;
            }

            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            var dictionary = dictionaries.FirstOrDefault(d => d.Id == dictId);
            if (dictionary == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.DictionaryNotFound"], ct);
                return;
            }

            if (dictionary.Name == "default")
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.CannotDeleteDefaultDictionary"], ct);
                return;
            }

            _pendingDeleteDict[chatId] = dictId;

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]{ InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonYes"], $"delete_dict_full:confirm_{dictId}") },
                new[]{ InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonNo"], _localizer["Worker.ButtonCancel"]) }
            });
            await _msg.SendText(chatId, _localizer["Worker.ConfirmDictionaryDeletion"], kb, ct);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery is { } callback)
            {
                // Для CallbackQuery тоже нужно установить культуру, если это возможно и необходимо
                var userForCallback = await _userRepo.GetByTelegramIdAsync(callback.From.Id);
                if (userForCallback != null)
                {
                    if (!string.IsNullOrWhiteSpace(userForCallback.Native_Language) &&
                        LanguageNameToCultureCodeMap.TryGetValue(userForCallback.Native_Language, out var cultureCode) &&
                        _supportedCultureStrings.Contains(cultureCode))
                    {
                        var cultureInfo = new CultureInfo(cultureCode);
                        CultureInfo.CurrentCulture = cultureInfo;
                        CultureInfo.CurrentUICulture = cultureInfo;
                    }
                    else
                    {
                        var fallbackCulture = new CultureInfo("en-US");
                        CultureInfo.CurrentCulture = fallbackCulture;
                        CultureInfo.CurrentUICulture = fallbackCulture;
                    }
                }
                // Если userForCallback == null, будет использована культура по умолчанию из Program.cs или en-US, если предыдущий Message успел ее установить.
                // Это редкий случай, но стоит иметь в виду.

                await HandleCallbackAsync(botClient, callback, ct);
                return;
            }

            if (update.Message is not { } message || message.Text is not { } text)
                return;

            var chatId = message.Chat.Id;
            var userTelegramId = message.From!.Id;
            var messageId = message.Id;
            try
            {
                var (user, isNewUser) = await EnsureUserAsync(message);

                // Установка культуры пользователя
                if (!string.IsNullOrWhiteSpace(user.Native_Language) &&
                    LanguageNameToCultureCodeMap.TryGetValue(user.Native_Language, out var cultureCode) &&
                    _supportedCultureStrings.Contains(cultureCode))
                {
                    var cultureInfo = new CultureInfo(cultureCode);
                    CultureInfo.CurrentCulture = cultureInfo;
                    CultureInfo.CurrentUICulture = cultureInfo;
                }
                else
                {
                    // Если язык пользователя не найден, не поддерживается или не указан, используем английский (en-US)
                    var fallbackCulture = new CultureInfo("en-US");
                    CultureInfo.CurrentCulture = fallbackCulture;
                    CultureInfo.CurrentUICulture = fallbackCulture;
                }

                if (isNewUser)
                {
                    await SendWelcomeAsync(user, chatId, ct);
                    return;
                }

                await filterMessages(message);
                // Handle keyboard buttons first
                var (handled, newState) = await HandleKeyboardCommandAsync(user, text, chatId, ct);
                if (handled)
                {
                    if (!string.IsNullOrEmpty(newState))
                    {
                        _userStates[userTelegramId] = newState;
                    }
                    return;
                }

                var lowerText = text.Trim().ToLowerInvariant();

                // Handle FSM states
                if (_userStates.TryGetValue(userTelegramId, out var state))
                {

                    _userStates.Remove(userTelegramId);
                    switch (state)
                    {
                        case "awaiting_nativelanguage":
                            await ProcessAddNativeLanguage(user, text, ct);
                            break;
                        case "awaiting_language":
                            await ProcessAddLanguage(user, text, ct);
                            break;
                        case "awaiting_addword":
                            await ProcessAddWord(user, text, ct);
                            break;
                        case "awaiting_remove_foreign":
                            await ProcessRemoveForeignLanguage(user, text, ct);
                            break;
                        case "awaiting_currentlanguage":
                            await ProcessChangeCurrentLanguage(user, text, ct);
                            break;
                        case "awaiting_editsearch":
                            await ProcessEditSearch(user, text, ct);
                            break;
                        case "awaiting_listdelete":
                            await ProcessDeleteList(user, text, ct);
                            break;
                        case "awaiting_create_dict_name":
                            await CreateDictionary(user, chatId, text, ct);
                            break;
                        case "awaiting_generation_theme_input":
                            await ProcessGenerationThemeInput(user, chatId, text, ct);
                            break;
                    }
                    return;
                }

                // Ensure languages are set
                if (string.IsNullOrWhiteSpace(user.Native_Language))
                {
                    _userStates[userTelegramId] = "awaiting_nativelanguage";
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterNativeLanguage"], ct);
                    return;
                }

                if (string.IsNullOrWhiteSpace(user.Current_Language))
                {
                    _userStates[userTelegramId] = "awaiting_language";
                    await _msg.SendInfoAsync(chatId, _localizer["Worker.WhatLanguageToLearn"], ct);
                    return;
                }

                var cmd = text.Trim().Split(' ')[0].ToLowerInvariant();

                // Text commands
                switch (cmd)
                {
                    case "/start":
                        await ProcessStartCommand(user, message, ct);
                        break;

                    case "/addword":
                        var langs = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        if (!langs.Any())
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.AddLanguageFirst"], ct);
                            return;
                        }
                        _userStates[userTelegramId] = "awaiting_addword";
                        await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterWordToRemember"], ct);
                        break;

                    case "/learn":
                        await StartLearningAsync(user, ct);
                        break;

                    case "/config":
                        await _keyboardFactory.ShowConfigMenuAsync(_botClient, chatId, user, ct);
                        break;

                    case "/addlanguage":
                        var parts = text.Split(' ', 2);
                        if (parts.Length < 2)
                        {
                            _userStates[userTelegramId] = "awaiting_language";
                            await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterLanguageName"], ct);
                        }
                        else
                        {
                            await ProcessAddLanguage(user, parts[1], ct);
                        }
                        break;

                    case "/removelanguage":
                        var rm = text.Split(' ', 2);
                        if (rm.Length < 2)
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.UsageRemoveLanguage"], ct);
                        }
                        else
                        {
                            await ProcessRemoveForeignLanguage(user, rm[1], ct);
                        }
                        break;

                    case "/listlanguages":
                        var all = await _languageRepo.GetAllAsync();
                        var list = all.Any()
                            ? string.Join("\n", all.Select(l => $"{l.Code} — {l.Name}"))
                            : _localizer["Worker.LanguageListEmpty"];
                        await botClient.SendMessage(chatId, list, cancellationToken: ct);
                        break;

                    case "/mylangs":
                        var my = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        if (!my.Any())
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.NoAddedLanguages"], ct);
                        else
                            await _msg.SendInfoAsync(chatId,
                                _localizer["Worker.YourLearningLanguages", string.Join("\n", my)], ct);
                        break;

                    case "/clearuserdata":
                        await _msg.SendSuccessAsync(chatId, _localizer["Worker.ResettingData"], ct);
                        await _userLangRepository.RemoveAllUserLanguages(user);
                        await _userWordRepo.RemoveAllUserWords(user);
                        await _dictionaryRepo.DeleteByUserAsync(user.Id);
                        await _userRepo.DeleteAsync(user.Id);
                        await _msg.SendSuccessAsync(chatId, _localizer["Worker.Done"], ct);
                        break;

                    case "/user":
                        var userLangs = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        var info = $"{message.From.FirstName}\n@{message.From.Username}\n" +
                                   $"Native: {user.Native_Language}\nCurrent: {user.Current_Language}\n" +
                                   string.Join(", ", userLangs);
                        await botClient.SendMessage(chatId, info, parseMode: ParseMode.MarkdownV2, cancellationToken: ct);
                        break;

                    case "/removeword":
                        var sw = text.Split(' ', 2);
                        if (sw.Length < 2)
                            await _msg.SendInfoAsync(chatId, _localizer["Worker.ExampleRemoveWord"], ct);
                        else
                        {
                            var ok = await _userWordRepo.RemoveUserWordAsync(user.Id, sw[1].Trim());
                            if (ok)
                                await _msg.SendInfoAsync(chatId, _localizer["Worker.WordXDeleted", sw[1]], ct);
                            else
                                await _msg.SendInfoAsync(chatId, _localizer["Worker.WordXNotFound", sw[1]], ct);
                        }
                        break;
                                                
                    case "/todo":
                        var todoContent = text.Substring(5).Trim();
                        if (string.IsNullOrWhiteSpace(todoContent))
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.UsageTodo"], ct);
                            break;
                        }
                        var split = todoContent.Split(':', 2);
                        var title = split[0].Trim();
                        var desc = split.Length > 1 ? split[1].Trim() : string.Empty;
                        var todo = new TodoItem
                        {
                            Id = Guid.NewGuid(),
                            User_Id = user.Id,
                            Title = title,
                            Description = desc,
                            Created_At = DateTime.UtcNow
                        };
                        await _todoRepo.AddAsync(todo);
                        await _msg.SendSuccessAsync(chatId, _localizer["Worker.TaskAdded"], ct);
                        break;

                    case "/todos":
                        var items = (await _todoRepo.GetAllAsync(user.Id)).ToList();
                        if (!items.Any())
                        {
                            await _msg.SendInfoAsync(chatId, _localizer["Worker.LanguageListEmpty"], ct); // Используем существующий ключ для "Список пуст"
                            break;
                        }
                        var sbList = new StringBuilder();
                        foreach (var it in items)
                        {
                            var t = TelegramMessageHelper.EscapeHtml(it.Title);
                            var d = TelegramMessageHelper.EscapeHtml(it.Description);
                            var link = string.IsNullOrEmpty(_appUrl) ? $"/todoitems/{it.Id}/complete" : $"{_appUrl}/todoitems/{it.Id}/complete";
                            if (!it.Is_Complete)
                                sbList.AppendLine($"<a href='{link}'>[✓]</a> <b>{t}</b> {d}");
                            else
                                sbList.AppendLine($"✔️ <b>{t}</b> {d}");
                        }
                        await _msg.SendText(new ChatId(chatId), sbList.ToString(), ct);
                        break;

                    case "/mywords":
                        await ShowMyWords(chatId, user, ct);
                        break;

                    case "/adminstat":
                        var pwd = text.Split(' ', 2).Skip(1).FirstOrDefault();
                        var env = Environment.GetEnvironmentVariable("PASSWORD");
                        if (!string.IsNullOrEmpty(env) && pwd == env)
                        {
                            await ShowAdminStatistics(chatId, ct);
                        }
                        else
                        {
                            await _msg.SendErrorAsync(chatId, _localizer["Worker.AccessDenied"], ct);
                        }
                        break;

                    default:
                        await _msg.SendErrorAsync(chatId, _localizer["Worker.UnknownCommand"], ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizer["Worker.ErrorProcessingUpdate"]); // Предполагаем, что такой ключ будет добавлен
                if (ex.Message.Contains("Translation")) // Эту специфичную проверку можно оставить или также обернуть в локализацию, если текст ошибки ثابتный
                {
                    await _msg.SendErrorAsync(chatId, ex.Message, ct); // Отправляем исходное сообщение об ошибке, т.к. оно может быть от внешней системы
                }
            }
        }

        private async Task ProcessGenerationThemeInput(User user, long chatId, string text, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.ThemeCannotBeEmpty"], ct);
                return;
            }
            var newWords = await _ai.GetWordByTheme(text, 20, user.Native_Language, user.Current_Language);
            if (newWords == null || !newWords.Items.Any())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.FailedToGetWordsForTheme"], ct);
                return;
            }
            InlineKeyboardButton[] buttons = new InlineKeyboardButton[] { };
            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            foreach (var item in dictionaries)
            {
                buttons = buttons.Append(InlineKeyboardButton.WithCallbackData(item.Name, $"fill_dict:{text}")).ToArray();
            }
            buttons = buttons.Append(InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonNewTheme", text], $"fill_dict:{text}")).ToArray();
            var msgString = _localizer["Worker.ReceivedXWordsForTheme", newWords.Items.Count, text];
            _translationCandidates[chatId] = newWords;
            await _msg.SendText(chatId, msgString, buttons, ct);

        }

        //Saves new words to the user's dictionary
        private async Task<bool> SaveNewWords(User user, List<TranslatedItem> items, string? dictName, CancellationToken ct)
        {
            try
            {
                if (items == null || items.Count == 0)
                    return true;

                var sourceLang = await _languageRepo.GetByNameAsync(items.First().OriginalLanguage);
                var targetLang = await _languageRepo.GetByNameAsync(items.First().TranslatedLanguage);

                foreach (var item in items)
                {
                    if (string.IsNullOrWhiteSpace(item.OriginalText) || string.IsNullOrWhiteSpace(item.TranslatedText))
                        continue;

                    var word = await _wordRepo.GetByTextAndLanguageAsync(item.OriginalText!, sourceLang!.Id);
                    if (word == null)
                    {
                        word = new Word
                        {
                            Id = Guid.NewGuid(),
                            Base_Text = item.OriginalText!,
                            Language_Id = sourceLang!.Id
                        };
                        await _wordRepo.AddWordAsync(word);
                    }

                    var translation = await _translationRepo.GetTranslationAsync(word.Id, targetLang!.Id);
                    if (translation == null)
                    {
                        translation = new Translation
                        {
                            Id = Guid.NewGuid(),
                            Word_Id = word.Id,
                            Language_Id = targetLang!.Id,
                            Text = item.TranslatedText!,
                            Examples = item.Example
                        };
                        await _translationRepo.AddTranslationAsync(translation);
                    }

                    await _userWordRepo.AddUserWordAsync(user.Id, word.Id, translation.Id);
                    await _dictionaryRepo.AddWordAsync(dictName ?? "default", word.Id, user.Id);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, _localizer["Worker.ErrorSavingWords"]);
                return false;
            }
        }

        private async Task CreateDictionary(User? user, long chatId, string dictName, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(dictName))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.DictionaryNameCannotBeEmpty"], ct);
                return;
            }
            await _dictionaryRepo.AddDictionaryAsync(new Dictionary
            {
                Id = Guid.NewGuid(),
                User_Id = user!.Id,
                Name = dictName
            });
            await _msg.SendSuccessAsync(chatId, _localizer["Worker.DictionaryXCreated", dictName], ct);
            _translationCandidates.Remove(chatId);
            await _msg.SendConfirmationDialog(chatId, _localizer["Worker.Add20WordsToDictionaryConfirmation", dictName], "fill_dict:" + dictName, _localizer["Worker.ButtonCancel"], ct);
        }

        private async Task ProcessChangeCurrentLanguage(User user, string text, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private async Task filterMessages(Message? message)
        {
            if (message == null) return;
            var keyboard = _keyboardFactory.GetMainMenu(); // Теперь не требует локализатора
            if (keyboard.Keyboard.Any(x => x.Any(c => c.Text.Contains(message.Text.Trim()))))
            {
                await _botClient.DeleteMessage(message.Chat.Id, message.Id);
            }
        }

        private async Task ShowMyWords(long chatId, User user, CancellationToken ct)
        {
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var langs = (await _userLangRepository.GetUserLanguagesAsync(user.Id)).ToList();

            // Если языков нет
            if (!langs.Any())
            {
                await _msg.SendText(new ChatId(chatId), _localizer["Worker.NoWordsInLanguage"], ct);
                await _msg.SendInfoAsync(chatId, _localizer["Worker.WhatLanguageToLearn"], ct);
                _userStates[chatId] = "awaiting_language";
                return;
            }

            // Локальный экранировщик HTML для заголовков и текста
            string Escape(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

            foreach (var lang in langs)
            {
                var words = (await _userWordRepo.GetWordsByUserId(user.Id, lang.Id)).ToList();
                var header = $"<b>{Escape(lang.Name)} ({words.Count})</b>";

                if (!words.Any())
                {
                    // Для пустого списка — просто заголовок и «Нет слов»
                    await _msg.SendText(new ChatId(chatId), _localizer["Worker.NoWordsHeader", header], ct);
                    continue;
                }

                if (words.Count <= 30)
                {
                    // Небольшой список — отправляем единым сообщением
                    var sb = new StringBuilder();
                    sb.AppendLine(header);
                    foreach (var w in words)
                    {
                        var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                        var right = tr?.Text ?? "-";
                        sb.AppendLine($"{Escape(w.Base_Text)} — {Escape(right)}");
                    }
                    await _msg.SendText(new ChatId(chatId), sb.ToString(), ct);
                }
                else
                {
                    // Длинный список — показываем слайдер
                    await _msg.SendText(new ChatId(chatId), _localizer["Worker.NavigateWordsWithButtons", header], ct);

                    // Первая карточка в слайдере
                    var first = words[0];
                    var firstTr = await _translationRepo.GetTranslationAsync(first.Id, native.Id);
                    var imgPath = await _imageService.GetImagePathAsync(first);
                    await _msg.ShowWordSlider(
                        new ChatId(chatId),
                        langId: lang.Id,
                        currentIndex: 0,
                        totalWords: words.Count,
                        word: first.Base_Text,
                        translation: firstTr?.Text ?? "-",
                        example: firstTr?.Examples ?? null,
                        category: lang.Name,
                        imageUrl: imgPath,
                        voiceLanguage: lang.Name,
                        ct: ct
                    );
                }
            }
        }

        private async Task ShowMyWordsForEdit(long chatId, User user, CancellationToken ct)
        {
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var langs = (await _userLangRepository.GetUserLanguagesAsync(user.Id)).ToList();

            var list = new List<(Guid Id, string Display)>();
            foreach (var lang in langs)
            {
                var words = (await _userWordRepo.GetWordsByUserId(user.Id, lang.Id)).ToList();
                foreach (var w in words)
                {
                    var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                    var display = $"{w.Base_Text} - {tr?.Text ?? "-"} ({lang.Name})";
                    list.Add((w.Id, display));
                }
            }

            if (!list.Any())
            {
                await _msg.SendText(new ChatId(chatId), _localizer["Worker.NoWordsToList"], ct);
                return;
            }

            _editListCache[chatId] = list;
            await SendEditListPage(chatId, 0, ct);
            _userStates[chatId] = "awaiting_listdelete";
        }

        private async Task ShowDictionaryWordsForEdit(long chatId, Guid dictId, User user, CancellationToken ct)
        {
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var words = (await _dictionaryRepo.GetWordsAsync(dictId)).ToList();

            var list = new List<(Guid Id, string Display)>();
            foreach (var w in words)
            {
                var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                var display = $"{w.Base_Text} - {tr?.Text ?? "-"}";
                list.Add((w.Id, display));
            }

            if (!list.Any())
            {
                await _msg.SendText(new ChatId(chatId), _localizer["Worker.NoWordsToList"], ct);
                return;
            }

            _editListCache[chatId] = list;
            await SendEditListPage(chatId, 0, ct);
            _pendingDeleteWordsDict[chatId] = dictId;
            _userStates[chatId] = "awaiting_listdelete";
        }

        private async Task ShowDictionariesForAction(long chatId, string action, CancellationToken ct)
        {
            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = (await _dictionaryRepo.GetByUserAsync(user.Id)).ToList();
            if (!dictionaries.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoDictionaries"], ct);
                return;
            }

            var buttons = dictionaries
                .Where(d => action != "delete_dict" || d.Name != "default")
                .Select(d =>
                    new[] { InlineKeyboardButton.WithCallbackData(d.Name == "default" ? _localizer["Worker.DictionaryNameDefault"] : d.Name, $"{action}:{d.Id}") })
                .ToArray();

            var kb = new InlineKeyboardMarkup(buttons);
            await _msg.SendText(chatId, _localizer["Worker.SelectWordPrompt"], kb, ct); // "Выберите словарь:" -> "Выберите слово:" (более общий ключ) или создать новый
        }

        private async Task SendEditListPage(long chatId, int page, CancellationToken ct)
        {
            if (!_editListCache.TryGetValue(chatId, out var list)) return;

            var totalPages = (int)Math.Ceiling(list.Count / (double)EditListPageSize);
            page = Math.Clamp(page, 0, Math.Max(totalPages - 1, 0));
            _editListPage[chatId] = page;

            var start = page * EditListPageSize;
            var end = Math.Min(start + EditListPageSize, list.Count);
            var sb = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                var idx = i - start + 1;
                sb.AppendLine($"{idx}. {TelegramMessageHelper.EscapeHtml(list[i].Display)}");
            }
            sb.AppendLine("_________________________"); // Эта строка может остаться или быть вынесена, если нужна локализация
            sb.AppendLine(_localizer["Worker.PageXofY", page + 1, totalPages] + Environment.NewLine);
            sb.AppendLine(_localizer["Worker.EnterWordNumbersToDelete"]);

            InlineKeyboardMarkup? keyboard = null;
            if (totalPages > 1)
            {
                var buttons = new List<InlineKeyboardButton>();
                if (page > 0)
                    buttons.Add(InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.BackButton"], $"dlistprev:{page - 1}")); // Используем ключ из TelegramMessageHelper
                if (page < totalPages - 1)
                    buttons.Add(InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.ForwardButton"], $"dlistnext:{page + 1}")); // Используем ключ из TelegramMessageHelper
                if (buttons.Any())
                    keyboard = new InlineKeyboardMarkup(new[] { buttons.ToArray() });
            }

            if (keyboard != null)
            {
                await _msg.SendText(new ChatId(chatId), sb.ToString(), keyboard, ct);
            }
            else
                await _msg.SendText(new ChatId(chatId), sb.ToString(), ct);
        }

        private async Task ProcessDeleteList(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            if (!_editListCache.TryGetValue(chatId, out var list))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.LanguageListEmpty"], ct); // Используем существующий ключ
                return;
            }

            var page = _editListPage.GetValueOrDefault(chatId);
            var start = page * EditListPageSize;

            var nums = text.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var ids = new List<Guid>();
            foreach (var n in nums)
            {
                if (int.TryParse(n, out var num))
                {
                    var idx = start + num - 1;
                    if (idx >= 0 && idx < list.Count)
                        ids.Add(list[idx].Id);
                }
            }

            if (!ids.Any())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.NumbersNotRecognized"], ct);
                return;
            }

            int removed = 0;
            foreach (var id in ids.Distinct())
            {
                if (await _userWordRepo.RemoveUserWordAsync(user.Id, id))
                {
                    removed++;
                    if (_pendingDeleteWordsDict.TryGetValue(chatId, out var dId))
                        await _dictionaryRepo.RemoveWordAsync(dId, id);
                }
            }

            await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordsDeletedCount", removed], ct);

            if (_pendingDeleteWordsDict.TryGetValue(chatId, out var dictId))
            {
                await ShowDictionaryWordsForEdit(chatId, dictId, user, ct);
            }
            else
            {
                await ShowMyWordsForEdit(chatId, user, ct);
            }
        }


        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiEx => _localizer["Worker.ErrorTelegramAPI", apiEx.Message],
                _ => exception.ToString() // Для неизвестных ошибок оставляем стандартное сообщение
            };
            _logger.LogError(errorMessage);
            // Возможно, стоит отправлять пользователю общее сообщение об ошибке, если это уместно
            // await _msg.SendErrorAsync(chatId, _localizer["Worker.GenericError"], ct);
            return Task.CompletedTask;
        }

   
        /// <summary>
        /// Сохраняет в БД новый Word + выбранные Translations + Examples.
        /// </summary>
        private async Task FinalizeAddWord(User user, Language? inputLang, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            if (!_pendingOriginalText.TryGetValue(chatId, out var originalText))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.WordToSaveNotFound"], ct);
                return;
            }
            bool isNative = _isNativeInput[chatId];
            var aiResult = _translationCandidates[chatId]; // TranslatedTextClass
            var translated_items = aiResult.Items;
              // Индексы выбранных переводов
            var translationIndices = _selectedTranslations[chatId];
            var examplesIndices = _selectedExamples[chatId];
            // Очистка временных данных
            _pendingOriginalText.Remove(chatId);
            _isNativeInput.Remove(chatId);
            _translationCandidates.Remove(chatId);
            _selectedTranslations.Remove(chatId);
            _selectedExamples.Remove(chatId);

            var native = await _languageRepo.GetByNameAsync(user.Native_Language);

            var current = (inputLang == null || inputLang.Id == native.Id) ? await _languageRepo.GetByNameAsync(user.Current_Language) : inputLang;

            if (isNative)
            {
                // Ввод на родном: для каждого варианта создаём своё слово + свой пример
                foreach (var idx in translationIndices)
                {
                    var translated_item = translated_items[idx];
                    var variant = translated_item.TranslatedText!;

                    var examplesStr = findExamplesByWord(aiResult, variant, examplesIndices);

                    var word = new Word
                    {
                        Id = Guid.NewGuid(),
                        Base_Text = variant,
                        Language_Id = current!.Id
                    };
                    await _wordRepo.AddWordAsync(word);

                    var tr = new Translation
                    {
                        Id = Guid.NewGuid(),
                        Word_Id = word.Id,
                        Language_Id = native!.Id,
                        Text = originalText,
                        Examples = examplesStr
                    };
                    await _translationRepo.AddTranslationAsync(tr);

                    await AddWordToUserDictionary(user, "default", tr, word);

                    var imgPath = await _imageService.GetImagePathAsync(word);
                    await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordAdded", word.Base_Text], ct);
                    await _msg.SendWordCardWithEdit(
                        chatId: new ChatId(chatId),
                        word: word.Base_Text,
                        translation: originalText,
                        wordId: word.Id,
                        example: examplesStr,
                        category: current.Name,
                        imageUrl: imgPath,
                        voiceLanguage: current.Name,
                        ct: ct);
                }
            }
            else
            {
                // Ввод на иностранном: одно слово + несколько переводов с их примерами
                var word = new Word
                {
                    Id = Guid.NewGuid(),
                    Base_Text = aiResult.Items?.FirstOrDefault()?.OriginalText ?? string.Empty,
                    Language_Id = current!.Id
                };
                await _wordRepo.AddWordAsync(word);
                Translation tr = await SaveTranslations(aiResult, translationIndices, examplesIndices, native, word);

                var imgPath = await _imageService.GetImagePathAsync(word);
                await AddWordToUserDictionary(user, "default", tr, word);
                await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordAdded", word.Base_Text], ct);
                await _msg.SendWordCardWithEdit(
                    chatId: new ChatId(chatId),
                    word: word.Base_Text,
                    translation: tr.Text,
                    wordId: word.Id,
                    example: tr.Examples,
                    category: current.Name,
                    imageUrl: imgPath,
                    voiceLanguage: current.Name,
                    ct: ct);
            }
        }

        private async Task<Translation> SaveTranslations(TranslatedTextClass aiResult, List<int> translationIndices, List<int> examplesIndices, Language native,  Word word)
        {
            var savedTrIds = new List<Guid>();
            var savedTexts = new List<string>();
            var savedExamples = new List<string>();
            foreach (var idx in translationIndices)
            {
                var itranslated_item = aiResult.Items[idx];
                if (string.IsNullOrEmpty(itranslated_item.TranslatedText)) continue;

                savedTexts.Add(itranslated_item.TranslatedText);

            }

            foreach (var idx in examplesIndices)
            {
                var iexample = aiResult.Items[idx].Example;
                if (!string.IsNullOrEmpty(iexample))
                {
                    savedExamples.Add(iexample);
                }
            }
            var tr = new Translation
            {
                Id = Guid.NewGuid(),
                Word_Id = word.Id,
                Language_Id = native!.Id,
                Text = string.Join(", ", savedTexts),
                Examples = string.Join("\n", savedExamples)
            };
            await _translationRepo.AddTranslationAsync(tr);
            return tr;
        }

        private string findExamplesByWord(TranslatedTextClass aiResult, string variant, List<int> examplesIndices)
        {
            string result = string.Empty;
            for (int i = 0; i < aiResult.Items.Count; i++)
            {
                var item = aiResult.Items[i];
                // Если перевод совпадает с вариантом и индекс в списке примеров
                if (item.TranslatedText == variant && examplesIndices.Contains(i))
                {
                    result+= item.Example ?? string.Empty;
                }

                // Если пример содержит вариант и индекс в списке примеров
                if (item.Example!= null && item.Example.Contains(variant) && examplesIndices.Contains(i))
                {
                    if (!result.Contains(item.Example))
                        result += item.Example ?? string.Empty;
                }
            }
            return result;
        }



        /// <summary>
        /// Запуск редактирования существующего слова: берём Word.Id, запускаем AI для baseText→native,
        /// показываем клавиатуру переводов.
        /// </summary>
        private async Task ProcessEditWord(User user, Guid wordId, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            var word = await _wordRepo.GetWordById(wordId);
            if (word == null || native == null || current == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.ErrorLoadingWord"], ct);
                return;
            }

            // AI перевод baseText→native
            var aiResult = await _ai.TranslateWordAsync(word.Base_Text, current.Name, native.Name);
            if (aiResult == null || !aiResult.IsSuccess())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.ErrorAiTranslation"], ct);
                return;
            }

            _pendingEditWordId[chatId] = wordId;
            _editTranslationCandidates[chatId] = aiResult;
            _selectedEditTranslations[chatId] = new List<int> { 0 };
            _selectedEditExamples[chatId] = new();

            await ShowEditTranslationOptions(chatId, aiResult, ct);
        }

        /// <summary>
        /// Показать inline-клавиатуру переводов (для редактирования).
        /// </summary>
        private async Task ShowEditTranslationOptions(long chatId, TranslatedTextClass aiResult, CancellationToken ct)
        {
            var variants = aiResult.Items
                .Select(t => t.TranslatedText)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
            var rows = variants
                .Select((t, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedEditTranslations[chatId].Contains(i) ? "✅ " : string.Empty) +
                      TelegramMessageHelper.EscapeHtml(t),
                callbackData: $"editSelectTrans:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonDone"], "editSelectTransDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.EditTranslationsPrompt"],
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct
            );
        }

        /// <summary>
        /// Показать inline-клавиатуру примеров (для редактирования).
        /// </summary>
        private async Task ShowEditExampleOptions(long chatId, TranslatedTextClass aiResult, CancellationToken ct)
        {
            var examples = aiResult.Items
                .Select(t => t.Example)
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct()
                .ToList();
            var rows = examples
                .Select((ex, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedEditExamples[chatId].Contains(i) ? "✅ " : string.Empty) + // Оставляем эмодзи или можно сделать ключи и для них
                      TelegramMessageHelper.EscapeHtml(ex),
                callbackData: $"editSelectEx:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonDone"], "editSelectExDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.EditExamplesPrompt"],
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct
            );
        }

        /// <summary>
        /// Сохраняет изменения: удаляет старые переводы, добавляет новые, обновляет UserWord.translation_id.
        /// </summary>
        private async Task FinalizeEditWord(User user, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            if (!_pendingEditWordId.TryGetValue(chatId, out var wordId))
            {
                await _msg.SendErrorAsync(chatId, "Не найдено слово для редактирования", ct);
                return;
            }

            // Получаем TranslatedTextClass и список элементов (каждый элемент содержит Text + Example)
            var aiResult = _editTranslationCandidates[chatId]; // TranslatedTextClass
            var items = aiResult.Items;

            // Индексы выбранных переводов
            var translationIndices = _selectedEditTranslations[chatId];
            var examplesIndices = _selectedEditExamples[chatId];

            // Очищаем кешированные состояния
            _pendingEditWordId.Remove(chatId);
            _editTranslationCandidates.Remove(chatId);
            _selectedEditTranslations.Remove(chatId);
            _selectedEditExamples.Remove(chatId);

            // Удаляем все старые переводы для этого слова
            await _translationRepo.RemoveByWordIdAsync(wordId);

            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var word = await _wordRepo.GetWordById(wordId);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            var tr = await SaveTranslations(aiResult, translationIndices, examplesIndices, native, word);

            await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordUpdated", word!.Base_Text], ct);
            var imgPath = await _imageService.GetImagePathAsync(word);
            await _msg.SendWordCardWithEdit(
                chatId: new ChatId(chatId),
                word: word.Base_Text,
                translation: tr.Text,
                wordId: word.Id,
                example: tr.Examples,
                category: current!.Name,
                imageUrl: imgPath,
                voiceLanguage: current!.Name,
                ct: ct);
        }

        private async Task ProcessEditSearch(User user, string query, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            query = query.Trim();
            if (string.IsNullOrEmpty(query))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.EmptyQuery"], ct);
                return;
            }

            var words = (await _userWordRepo.GetWordsByUserId(user.Id))
                .Where(w => w.Base_Text.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!words.Any())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.NothingFound"], ct);
                return;
            }

            var buttons = words.Select(w =>
                new[] { InlineKeyboardButton.WithCallbackData(w.Base_Text, $"startedit:{w.Id}") }).ToList();

            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.SelectWordPrompt"],
                replyMarkup: new InlineKeyboardMarkup(buttons),
                cancellationToken: ct);
        }


        /// <summary>
        /// Обрабатывает навигацию «Назад» / «Вперед» для слайдера слов.
        /// Ожидает callback.Data в формате "prev:LANG_ID:NEW_INDEX" или "next:LANG_ID:NEW_INDEX".
        /// </summary>
        private async Task HandleSliderNavigationAsync(CallbackQuery callback, User user, string[] parts, CancellationToken ct)
        {
            var chatId = callback.Message!.Chat.Id;

            if (parts.Length < 3
                || !int.TryParse(parts[1], out var langId)
                || !int.TryParse(parts[2], out var newIndex))
            {
                return;
            }

            var words = (await _userWordRepo.GetWordsByUserId(user.Id, langId)).ToList();
            if (newIndex < 0 || newIndex >= words.Count)
                return;

            var word = words[newIndex];
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var tr = await _translationRepo.GetTranslationAsync(word.Id, native.Id);
            var lang = (await _userLangRepository.GetUserLanguagesAsync(user.Id))
                       .FirstOrDefault(l => l.Id == langId);
            var category = lang?.Name ?? string.Empty;

            var imgPath = await _imageService.GetImagePathAsync(word);
            await _msg.ShowWordSlider(
                new ChatId(chatId),
                langId: langId,
                currentIndex: newIndex,
                totalWords: words.Count,
                word: word.Base_Text,
                translation: tr?.Text,
                example: tr?.Examples,
                category: category,
                imageUrl: imgPath,
                voiceLanguage: lang?.Name,
                ct: ct
            );
        }

        private async Task ProcessSwitchLanguage(CallbackQuery callback, long chatId, User? user, string[] parts, CancellationToken ct)
        {
            // Извлекаем GUID языка
            var langIdPart = parts[1];
            if (!int.TryParse(langIdPart, out var newLangId))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callback.Id,
                    text: _localizer["Worker.InvalidLanguageId"],
                    cancellationToken: ct
                );
                return;
            }

            // Проверяем, что юзер действительно изучает этот язык
            var userLangs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
            if (!userLangs.Any(lg => lg.Id == newLangId))
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQueryId: callback.Id,
                    text: _localizer["Worker.LanguageNotOnYourList"],
                    cancellationToken: ct
                );
                return;
            }
            var newUserLang = userLangs.First(lg => lg.Id == newLangId);
            // Сохраняем новый текущий язык
            user.Current_Language = newUserLang.Name;
            await _userRepo.UpdateAsync(user);

            // Подтверждаем выбор и удаляем inline-клавиатуру
            await _botClient.EditMessageReplyMarkup(
                chatId: chatId,
                messageId: callback.Message.MessageId,
                replyMarkup: null,
                cancellationToken: ct
            );

            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.CurrentLanguageSwitched", userLangs.First(lg => lg.Id == newLangId).Name],
                cancellationToken: ct
            );

            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: _localizer["Worker.LanguageChangedSuccessfully"],
                cancellationToken: ct
            );
        }

        /// <summary>
        /// Шлёт юзеру список его языков в виде inline-кнопок
        /// </summary>
        private async Task HandleSwitchLanguageCommandAsync(User user, long chatId, CancellationToken ct)
        {
            var langs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
            if (!langs.Any())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.NoLanguagesToSwitch"], ct);
                return;
            }

            var buttons = langs
                .Select(lg =>
                    InlineKeyboardButton.WithCallbackData(
                        text: lg.Name,
                        callbackData: $"switch_lang:{lg.Id}"
                    ))
                .Chunk(2) // по 2 кнопки в строке
                .Select(row => row.ToArray())
                .ToArray();

            var keyboard = new InlineKeyboardMarkup(buttons);

            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.SelectCurrentLanguagePrompt"],
                replyMarkup: keyboard,
                cancellationToken: ct
            );
        }

        
        private async Task StartLearningAsync(User user, CancellationToken ct)
        {
            await SendNextLearningWordAsync(user, user.Telegram_Id, ct);
        }

        private async Task UpdateLearningProgressAsync(User user, Guid wordId, bool success, CancellationToken ct)
        {
            var prog = await _progressRepo.GetAsync(user.Id, wordId)
                  ?? new UserWordProgress { User_Id = user.Id, Word_Id = wordId };

            _sr.UpdateProgress(prog, success);
            await _progressRepo.InsertOrUpdateAsync(prog);
            var word = await _wordRepo.GetWordById(wordId);
            var translation = await _translationRepo.GetTranslationAsync(wordId, user.Native_Language!);
            var imgPath = await _imageService.GetImagePathAsync(word);
            var lang = await _languageRepo.GetByIdAsync(word.Language_Id);
            if (success)
            {
                await _msg.SendSuccessAsync(user.Telegram_Id, _localizer["Worker.CorrectAnswer", word.Base_Text, translation.Text], ct);
            }
            else
            {
                await _msg.SendErrorAsync(user.Telegram_Id, _localizer["Worker.IncorrectAnswer", word.Base_Text, translation.Text], ct);
            }

            await _msg.SendWordCardAsync(user.Telegram_Id, word.Base_Text, translation.Text, translation.Examples, imgPath, lang?.Name, ct);
            //отправка карточки и переход к next
           // await SendNextLearningWordAsync(user, user.Telegram_Id, ct);
        }

        private async Task MarkWordAsKnownAsync(User user, Guid wordId, CancellationToken ct)
        {
            var prog = await _progressRepo.GetAsync(user.Id, wordId) ?? new UserWordProgress
            {
                User_Id = user.Id,
                Word_Id = wordId
            };

            prog.Repetition = 8;
            prog.Interval_Hours = 24 * 365 * 10; // ~10 years
            prog.Ease_Factor = Math.Max(prog.Ease_Factor, 2.5);
            prog.Last_Review = DateTime.UtcNow;
            prog.Next_Review = DateTime.UtcNow.AddYears(100);

            await _progressRepo.InsertOrUpdateAsync(prog);

            var word = await _wordRepo.GetWordById(wordId);
            if (word != null)
            {
                await _msg.SendSuccessAsync(user.Telegram_Id, _localizer["Worker.WordMarkedAsLearned", word.Base_Text], ct);
            }
        }

        private async Task SendNextLearningWordAsync(User user, long chatId, CancellationToken ct)
        {
            var currentLang = await _languageRepo.GetByNameAsync(user.Current_Language!);
            var all = await _userWordRepo.GetWordsByUserId(user.Id);
            all = all.Where(w => w.Language_Id == currentLang.Id);
            var due = new List<Word>();

            foreach (var w in all)
            { 
                var prog = await _progressRepo.GetAsync(user.Id, w.Id);
                // Новые слова (prog==null) или просроченные
                if (prog == null || prog.Next_Review <= DateTime.UtcNow)
                    due.Add(w);
            }

            if (!due.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NothingToRepeat"], ct);
                return;
            }

            var rnd = new Random();
            var word = due[rnd.Next(due.Count)];
            if (user.Prefer_Multiple_Choice)
                await ShowMultipleChoiceAsync(user, word, ct);
            else
                await ShowBinaryChoiceAsync(chatId, word, ct);
        }


        private async Task ShowMultipleChoiceAsync(User user, Word word, CancellationToken ct)
        {
            // 1) Собираем варианты: первый — верный, остальные — «отвлекающие»
            var native_lang = await _languageRepo.GetByNameAsync(user.Native_Language);
            var word_native = await _translationRepo.GetTranslationAsync(word.Id, native_lang.Id);
            var voice_language = await _languageRepo.GetByIdAsync(word.Language_Id);
            if (word_native == null) throw new Exception("GetTranslationAsync = null, ShowMultipleChoiceAsync"); // Эту ошибку лучше не локализовать, т.к. она для разработчика
            var variants = await _ai.GetVariants(word.Base_Text, word_native.Text, native_lang.Name);
            var correct = variants.First();
            
            // 2) Перемешиваем и строим InlineKeyboardMarkup
            var rnd = new Random();
            var shuffled = variants.OrderBy(_ => rnd.Next()).ToArray();
            var buttons = shuffled
                .Select(v =>
                    InlineKeyboardButton.WithCallbackData(
                        text: v,
                        callbackData: (v == correct
                            ? $"mc:correct:{word.Id}"
                            : $"mc:wrong:{word.Id}")
                    )
                )
                .ToArray();
            // Разбиваем на 2 колонки
            var rows = buttons.Chunk(2).ToList();
            rows.Insert(0, new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonKnowSkip"], $"mc:skip:{word.Id}") });
            var keyboard = new InlineKeyboardMarkup(rows);
            var filePath = FrameGenerator.GeneratePngFramedText(word.Base_Text, 200, 100, 16);
            string msg_text = _localizer["Worker.ChooseCorrectTranslationFor"] + Environment.NewLine;
            await _msg.SendPhotoWithCaptionAsync(user.Telegram_Id, filePath, msg_text, word.Base_Text, voice_language.Name, keyboard, ct);
        }

        private async Task ShowBinaryChoiceAsync(long chatId, Word word, CancellationToken ct)
        {
            var inline = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonKnowSkip"], $"learn:skip:{word.Id}") },
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonRemembered"], $"learn:rem:{word.Id}") },
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonNotRemembered"], $"learn:fail:{word.Id}") }
            });
            var voice_language = await _languageRepo.GetByIdAsync(word.Language_Id);
            string escapedWordBaseText = TelegramMessageHelper.EscapeHtml(word.Base_Text ?? string.Empty);
            string msg_text = _localizer["Worker.TranslateWordPrompt"] + Environment.NewLine;
            var filePath = FrameGenerator.GeneratePngFramedText(escapedWordBaseText, 200, 100, 16);
            await _msg.SendPhotoWithCaptionAsync(chatId, filePath, msg_text, word.Base_Text, voice_language.Name, inline, ct);
        }


        private async Task ProcessRemoveForeignLanguage(User user, string code, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var lang = await _languageRepo.GetByCodeAsync(code);
            if (lang == null) { await _msg.SendErrorAsync(chatId, _localizer["Worker.LanguageXNotFound", code], ct); return; }
            await _userLangRepository.RemoveUserLanguageAsync(user.Id, lang.Id);
            await _msg.SendSuccessAsync(chatId, _localizer["Worker.LanguageXRemoved", lang.Name], ct);
        }

        private async Task ProcessAddWord(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            var userLangs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
            userLangs = userLangs.Append(native);
            if (native == null || current == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.LanguagesNotSetup"], ct);
                return;
            }

            // 1) Определяем язык ввода
            var inputLangName = await _ai.GetLangName(text, userLangs);
            if (string.IsNullOrWhiteSpace(inputLangName) || inputLangName.ToLower() == "error")
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.CouldNotDetectLanguage"], ct);
                return;
            }
            var inputLang = await _languageRepo.GetByNameAsync(inputLangName);
            if (inputLang == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.LanguageXNotInDb", inputLangName], ct);
                return;
            }
            bool isNativeInput = inputLang.Id == native.Id;

            // 2) Проверяем, нет ли уже этого слова/перевода в базе
            if (!isNativeInput)
            {
                // Пользователь ввёл слово на иностранном языке
                var existingWord = await _wordRepo.GetByTextAndLanguageAsync(text, inputLang.Id);
                if (existingWord != null)
                {
                    // Слово есть в базе — проверим у пользователя
                    var has = await _userWordRepo.UserHasWordAsync(user.Id, existingWord.Base_Text);
                    if (has)
                    {
                        await _msg.SendInfoAsync(chatId, _localizer["Worker.WordAlreadyInYourDictionary", text], ct);
                    }
                    else
                    {
                        // Привязываем к пользователю
                        Translation? translation = await _translationRepo.GetTranslationAsync(existingWord.Id, inputLang.Id);//TODO check if translation is null and create it if needed
                        await AddWordToUserDictionary(user, "default", translation, existingWord);
                        await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordXAddedToDefaultDictionary", text, translation?.Text], ct);
                    }
                    return;
                }
            }
            else
            {
                // Пользователь ввёл слово на родном — ищем его среди переводов
                var translations = await _translationRepo.FindWordByText(text);
                var match = translations?.FirstOrDefault(tr => tr.Language_Id == native.Id);
                if (match != null)
                {
                    // Нашли перевод — получаем базовое слово
                    var foreignWord = await _wordRepo.GetWordById(match.Word_Id);
                    if (foreignWord != null && foreignWord.Language_Id == current.Id)//чтобы не было ситуации, что пользователь ввёл перевод на родном языке, а в базе есть слово на другом иностранном
                    {
                        var has = await _userWordRepo.UserHasWordAsync(user.Id, foreignWord.Base_Text);
                        if (has)
                        {
                            await _msg.SendInfoAsync(chatId, _localizer["Worker.WordAlreadyInYourDictionary", foreignWord.Base_Text], ct);
                        }
                        else
                        {
                            await AddWordToUserDictionary(user,"default", match, foreignWord);

                            var wordImage = await _imageRepo.GetByWordAsync(foreignWord.Id);
                            if (wordImage == null || !File.Exists(wordImage.FilePath))
                            {
                                // Если нет изображения, пытаемся получить его из интернета
                                var imgPath = await _imageService.GetImagePathAsync(foreignWord);
                                if (imgPath != null)
                                {
                                    await _msg.SendWordCardWithEdit(
                                        chatId: new ChatId(chatId),
                                        word: foreignWord.Base_Text,
                                        wordId: foreignWord.Id,
                                        category: current.Name,
                                        translation: text,
                                        imageUrl: imgPath,
                                        voiceLanguage: current.Name,
                                        ct: ct);
                                }
                            }
                            await _msg.SendSuccessAsync(chatId, _localizer["Worker.WordXAddedToDefaultDictionary", foreignWord.Base_Text, text], ct);
                        }
                        return;
                    }
                }
            }

            // 3) Иначе — запускаем AI-перевод
            var aiResult = isNativeInput
                ? await _ai.TranslateWordAsync(text, native.Name, current.Name) //родной → иностранный
                : await _ai.TranslateWordAsync(text, inputLang.Name, native.Name);// иностранный(не только текущий в изучении) → родной
            if (aiResult == null || !aiResult.IsSuccess())
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.ErrorAiTranslation"], ct);
                return;
            }

            // Получаем список вариантов и примеров из Items
            var items = aiResult.Items;
            var variants = items
                .Select(i => i.TranslatedText)
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();
            var examples = items
                .Select(i => i.Example)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct()
                .ToList();

            if (variants.Count == 1 && examples.Count <= 1)
            {
                _pendingOriginalText[chatId] = text;
                _isNativeInput[chatId] = isNativeInput;
                _translationCandidates[chatId] = aiResult;
                _selectedTranslations[chatId] = new List<int> { 0 };
                _selectedExamples[chatId] = examples.Count == 1 ? new List<int> { 0 } : new List<int>();
                _inputLanguage[chatId] = inputLang;
                await FinalizeAddWord(user, inputLang, ct);
                return;
            }
            _inputLanguage[chatId] = inputLang;
            _pendingOriginalText[chatId] = text;
            _isNativeInput[chatId] = isNativeInput;
            _translationCandidates[chatId] = aiResult;
            _selectedTranslations[chatId] = new List<int> { 0 };
            _selectedExamples[chatId] = new List<int>();

            await ShowTranslationOptions(chatId, aiResult, ct);
        }

        private async Task AddWordToUserDictionary(User user,string dictionaryName, Translation match, Word foreignWord)
        {
            await _userWordRepo.AddUserWordAsync(user.Id, foreignWord.Id, match.Id);
            await _dictionaryRepo.AddWordAsync(dictionaryName, foreignWord.Id, user.Id);
        }

        /// <summary>
        /// Показать inline-клавиатуру переводов (для добавления).
        /// </summary>
        private async Task ShowTranslationOptions(long chatId, TranslatedTextClass aiResult, CancellationToken ct)
        {
            var variants = aiResult.Items
                .Select(i => i.TranslatedText ?? string.Empty)
                .ToList();

            var rows = variants
                .Select((t, i) => new[] {
                    InlineKeyboardButton.WithCallbackData(
                        text: (_selectedTranslations[chatId].Contains(i) ? "✅ " : string.Empty) + TelegramMessageHelper.EscapeHtml(t),
                        callbackData: $"selectTrans:{i}"
                    )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonDone"], "selectTransDone") });

            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.ChooseTranslationsPrompt"],
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct
            );
        }
        /// <summary>
        /// Показать inline-клавиатуру примеров (для добавления).
        /// </summary>
        private async Task ShowExampleOptions(long chatId, TranslatedTextClass aiResult, CancellationToken ct)
        {
            var examples = aiResult.Items
                .Select(i => i.Example)
                .Where(e => !string.IsNullOrEmpty(e))
                .Distinct()
                .ToList();

            var rows = examples
                .Select((ex, i) => new[] {
                    InlineKeyboardButton.WithCallbackData(
                        text: (_selectedExamples[chatId].Contains(i) ? "✅ " : string.Empty) + TelegramMessageHelper.EscapeHtml(ex!), // Эмодзи пока оставляем
                        callbackData: $"selectEx:{i}"
                    )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonDone"], "selectExDone") });

            await _botClient.SendMessage(
                chatId: chatId,
                text: _localizer["Worker.ChooseExamplesPrompt"],
                parseMode: ParseMode.Html,
                replyMarkup: new InlineKeyboardMarkup(rows),
                cancellationToken: ct
            );
        }


        private async Task ProcessAddNativeLanguage(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var name = await _ai.GetLangName(text);
            if (name.ToLowerInvariant() == "error")
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.CouldNotRecognizeLanguage"], ct);
                return;
            }
            var lang = await _languageRepo.GetByNameAsync(name);
            user.Native_Language = lang!.Name;
            await _userRepo.UpdateAsync(user);
            await _botClient.SendMessage(chatId, _localizer["Worker.NativeLanguageSetTo", lang.Name], cancellationToken: ct);
        }

        private async Task ProcessAddLanguage(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var name = await _ai.GetLangName(text);
            if (name.ToLowerInvariant() == "error")
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.CouldNotRecognizeLanguage"], ct);
                return;
            }
            var lang = await _languageRepo.GetByNameAsync(name);
            if (lang == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.LanguageXNotInDb", name], ct);
                return;
            }
            await _userLangRepository.AddUserLanguageAsync(user.Id, lang!.Id);
            user.Current_Language = lang.Name;
            await _userRepo.UpdateAsync(user);
            await _botClient.SendMessage(chatId,
                _localizer["Worker.LanguageXAdded", lang.Name], replyMarkup: _keyboardFactory.GetMainMenu(), cancellationToken: ct);
        }

        private async Task ProcessStartCommand(User user, Message message, CancellationToken ct)
        {
            var chatId = message.Chat.Id;
            await SendWelcomeAsync(user, chatId, ct);
        }

        private async Task SendWelcomeAsync(User user, long chatId, CancellationToken ct)
        {
            await _msg.SendText(chatId, _localizer["Worker.WelcomeMessage"], ct);
            await _keyboardFactory.ShowMainMenuAsync(_botClient, chatId, ct);

            if (string.IsNullOrWhiteSpace(user.Native_Language))
            {
                _userStates[user.Telegram_Id] = "awaiting_nativelanguage";
                await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterNativeLanguage"], ct);
                return;
            }

            if (string.IsNullOrWhiteSpace(user.Current_Language))
            {
                _userStates[user.Telegram_Id] = "awaiting_language";
                await _msg.SendInfoAsync(chatId, _localizer["Worker.WhatLanguageToLearn"], ct);
                return;
            }

            _userStates[user.Telegram_Id] = "awaiting_generation_theme_input";
            await _msg.SendInfoAsync(chatId, _localizer["Worker.EnterThemeToCreateDictionary"], ct);
        }

        private async Task<(User user, bool isNew)> EnsureUserAsync(Message message)
        {
            var user = await _userRepo.GetByTelegramIdAsync(message.From!.Id);
            var isNew = user == null;
            if (user == null)
            {
                var lang = await _languageRepo.GetByCodeAsync(message.From.LanguageCode);
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Telegram_Id = message.From.Id,
                    Native_Language = lang?.Name ?? string.Empty,
                    First_Name = message.From.FirstName,
                    Last_Name = message.From.LastName,
                    User_Name = message.From.Username,
                    Is_Premium = message.From.IsPremium,
                    Last_Seen = DateTime.UtcNow
                };
                await _userRepo.AddAsync(user);
            }
            else
            {
                user.First_Name = message.From.FirstName;
                user.Last_Name = message.From.LastName;
                user.User_Name = message.From.Username;
                user.Is_Premium = message.From.IsPremium;
                user.Last_Seen = DateTime.UtcNow;
                await _userRepo.UpdateAsync(user);
            }

            return (user, isNew);
        }


        private async Task ShowStatisticsAsync(User user, ChatId chatId, CancellationToken ct)
        {
            // 1) Собираем все слова пользователя
            var allWords = (await _userWordRepo.GetWordsByUserId(user.Id)).ToList();
            int totalWords = allWords.Count;

            // 2) Собираем прогресс для всех слов
            var progresses = (await _progressRepo.GetByUserAsync(user.Id)).ToList();

            // 3) Считаем fully learned (Repetition >= 3) и in progress
            int fullyLearned = progresses.Count(p => p.Repetition >= 8);//TODO set limit to 8 repetitions for fully learned
            int inProgress = totalWords - fullyLearned;

            // 4) Стартуем сборку текста
            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.TotalStatisticsHeader"]);
            sb.AppendLine(_localizer["Worker.TotalWords", totalWords]);
            sb.AppendLine(_localizer["Worker.FullyLearnedWords", fullyLearned]);
            sb.AppendLine(_localizer["Worker.WordsInProgress", inProgress]);
            sb.AppendLine();

            // 5) Топ-10 самых «сложных» — сортируем по наименьшему числу повторений
            var hardest = progresses
                .Where(p => p.Repetition > 0 && p.Interval_Hours > 0 && p.Ease_Factor > 0)
                .OrderBy(p => p.Ease_Factor)
                .Take(10)
                .ToList();

            if (hardest.Any())
            {
                sb.AppendLine(_localizer["Worker.Top10HardestWords"]);
                foreach (var p in hardest)
                {
                    // Получаем текст слова
                    var word = await _wordRepo.GetWordById(p.Word_Id);
                    var wordText = word?.Base_Text;

                    var displayWordText = !string.IsNullOrEmpty(wordText)
                        ? TelegramMessageHelper.EscapeHtml(wordText)
                        : _localizer["Worker.UnknownLanguage"]; // "[Unknown Word]" -> localized

                    var remaining = p.Next_Review - DateTime.UtcNow;
                    string timeRemaining = remaining.TotalDays >= 1
                        ? _localizer["Worker.TimeRemainingDaysHours", (int)remaining.TotalDays, remaining.Hours]
                        : _localizer["Worker.TimeRemainingHoursMinutes", (int)remaining.TotalHours, remaining.Minutes];

                    sb.AppendLine(_localizer["Worker.HardWordDisplay",
                        displayWordText,
                        p.Repetition,
                        Math.Round(p.Ease_Factor, 2),
                        timeRemaining]);
                }
            }
            else
            {
                sb.AppendLine(_localizer["Worker.NoWords"]);
            }

            // 6) Отправляем одним сообщением
            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        // === New stub methods ===

        private async Task ShowTodayStatistics(User user, ChatId chatId, CancellationToken ct)
        {
            // Calculate statistics only for words reviewed today
            var today = DateTime.UtcNow.Date;
            var progresses = (await _progressRepo.GetByUserAsync(user.Id))
                .Where(p => p.Last_Review.HasValue && p.Last_Review.Value.Date == today)
                .ToList();

            if (!progresses.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoActivityToday"], ct);
                return;
            }

            int reviewed = progresses.Count;
            int learned = progresses.Count(p => p.Repetition >= 8);

            var hardest = progresses
                .Where(p => p.Ease_Factor > 0)
                .OrderBy(p => p.Ease_Factor)
                .Take(5)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.TodayStatisticsHeader"]);
            sb.AppendLine(_localizer["Worker.WordsRepeatedToday", reviewed]);
            sb.AppendLine(_localizer["Worker.WordsLearnedToday", learned]);

            if (hardest.Any())
            {
                sb.AppendLine();
                sb.AppendLine(_localizer["Worker.HardWordsHeader"]);
                foreach (var p in hardest)
                {
                    var word = await _wordRepo.GetWordById(p.Word_Id);
                    if (word != null)
                        sb.AppendLine(_localizer["Worker.HardWordSimple", TelegramMessageHelper.EscapeHtml(word.Base_Text)]);
                }
            }

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task ShowStatisticsByLanguages(User user, ChatId chatId, CancellationToken ct)
        {
            var languages = (await _userLangRepository.GetUserLanguagesAsync(user.Id)).ToList();
            if (!languages.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoAddedLanguages"], ct);
                return;
            }

            var progressMap = (await _progressRepo.GetByUserAsync(user.Id)).ToDictionary(p => p.Word_Id);

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.StatisticsByLanguageHeader"]);

            foreach (var lang in languages)
            {
                var words = (await _userWordRepo.GetWordsByUserId(user.Id, lang.Id)).ToList();
                int total = words.Count;
                int learned = words.Count(w => progressMap.TryGetValue(w.Id, out var p) && p.Repetition >= 8);
                int inProgress = total - learned;

                sb.AppendLine(_localizer["Worker.LanguageStats", TelegramMessageHelper.EscapeHtml(lang.Name), total, learned, inProgress]);
            }

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task ShowDictionariesByTopics(long chatId, CancellationToken ct)
        {
            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = (await _dictionaryRepo.GetByUserAsync(user.Id)).ToList();
            if (!dictionaries.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoDictionaries"], ct);
                return;
            }

            var inline = _keyboardFactory.GetDictionaryListInline(dictionaries);
            await _msg.SendText(chatId, _localizer["Worker.DictionariesByTopicsHeader"], inline, ct);
            
        }

        private async Task ShowDictionariesByLanguages(long chatId, CancellationToken ct)
        {
            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = (await _dictionaryRepo.GetByUserAsync(user.Id)).ToList();
            if (!dictionaries.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoDictionaries"], ct);
                return;
            }

            var langGroups = new Dictionary<string, List<Models.Dictionary>>();
            foreach (var d in dictionaries)
            {
                var words = (await _dictionaryRepo.GetWordsAsync(d.Id)).ToList();
                if (!words.Any())
                {
                    if (!langGroups.TryGetValue(_localizer["Worker.UnknownLanguage"], out var unknownList))
                    {
                        unknownList = new List<Models.Dictionary>();
                        langGroups[_localizer["Worker.UnknownLanguage"]] = unknownList;
                    }
                    unknownList.Add(d);

                    continue;
                }

                var langId = words.First().Language_Id;
                var lang = await _languageRepo.GetByIdAsync(langId);
                var key = lang?.Name ?? _localizer["Worker.UnknownLanguage"];

                if (!langGroups.TryGetValue(key, out var langList))
                {
                    langList = new List<Models.Dictionary>();
                    langGroups[key] = langList;
                }
                langList.Add(d);
            }

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.DictionariesByLanguageHeader"]);
            foreach (var kvp in langGroups)
            {
                sb.AppendLine($"\n<b>{TelegramMessageHelper.EscapeHtml(kvp.Key)}</b>");
                foreach (var d in kvp.Value)
                    sb.AppendLine(_localizer["Worker.DictionaryEntry", TelegramMessageHelper.EscapeHtml(d.Name), d.Id]);
            }

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task ResetAllWordProgress(long chatId, User user, CancellationToken ct)
        {
            var progresses = await _progressRepo.GetByUserAsync(user.Id);
            foreach (var p in progresses)
            {
                p.Repetition = 0;
                p.Interval_Hours = 0;
                p.Ease_Factor = 2.5;
                p.Next_Review = DateTime.UtcNow;
                await _progressRepo.InsertOrUpdateAsync(p);
            }

            await _msg.SendSuccessAsync(chatId, _localizer["Worker.AllWordProgressReset"], ct);
        }

        private async Task ShowProfileInfo(User user, ChatId chatId, CancellationToken ct)
        {
            var langs = (await _userLangRepository.GetUserLanguageNamesAsync(user.Id)).ToList();
            var totalWords = (await _userWordRepo.GetWordsByUserId(user.Id)).Count();
            var learningMode = user.Prefer_Multiple_Choice ? _localizer["Worker.ProfileLearningModeMultipleChoice"] : _localizer["Worker.ProfileLearningModeBinary"];

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.UserProfileHeader"]);
            sb.AppendLine(_localizer["Worker.ProfileId", user.Telegram_Id]);
            sb.AppendLine(_localizer["Worker.ProfileNativeLanguage", TelegramMessageHelper.EscapeHtml(user.Native_Language)]);
            sb.AppendLine(_localizer["Worker.ProfileCurrentLanguage", TelegramMessageHelper.EscapeHtml(user.Current_Language ?? user.Native_Language)]);
            sb.AppendLine(_localizer["Worker.ProfileLearningMode", learningMode]);
            sb.AppendLine();
            sb.AppendLine(_localizer["Worker.ProfileStudiedLanguages", (langs.Any() ? string.Join(", ", langs) : _localizer["Worker.ProfileNoStudiedLanguages"])]);
            sb.AppendLine(_localizer["Worker.ProfileTotalWords", totalWords]);

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task ResetProfileStatistics(User user, ChatId chatId, CancellationToken ct)
        {
            var progresses = await _progressRepo.GetByUserAsync(user.Id);
            foreach (var p in progresses)
            {
                p.Repetition = 0;
                p.Interval_Hours = 0;
                p.Ease_Factor = 2.5;
                p.Next_Review = DateTime.UtcNow;
                await _progressRepo.InsertOrUpdateAsync(p);
            }

            await _msg.SendSuccessAsync(chatId, _localizer["Worker.AllStatsReset"], ct);
        }

        private async Task ShowAdminStatistics(ChatId chatId, CancellationToken ct)
        {
            var users = (await _userRepo.GetAllUsersAsync()).ToList();
            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.AdminUsersHeader"]);

            foreach (var u in users)
            {
                var count = await _userWordRepo.GetWordCountByUserId(u.Id);
                var dicts = await _dictionaryRepo.GetByUserAsync(u.Id);
                var dictNames = string.Join(", ", dicts.Select(d => d.Name));
                sb.AppendLine(_localizer["Worker.AdminUserInfoLine1", u.Telegram_Id, TelegramMessageHelper.EscapeHtml(u.User_Name ?? string.Empty), TelegramMessageHelper.EscapeHtml(u.First_Name ?? string.Empty), TelegramMessageHelper.EscapeHtml(u.Last_Name ?? string.Empty), u.Last_Seen, u.Is_Premium]);
                sb.AppendLine(_localizer["Worker.AdminUserInfoLine2", TelegramMessageHelper.EscapeHtml(u.Native_Language), TelegramMessageHelper.EscapeHtml(u.Current_Language ?? string.Empty), u.Prefer_Multiple_Choice]);
                sb.AppendLine(_localizer["Worker.AdminUserWords", count]);
                sb.AppendLine(_localizer["Worker.AdminUserDictionaries", dictNames]);
                sb.AppendLine();
            }

            var total = await _wordRepo.GetTotalCountAsync();
            var byLang = await _wordRepo.GetCountByLanguageAsync();

            sb.AppendLine(_localizer["Worker.AdminTotalWords", total]);
            foreach (var kvp in byLang)
                sb.AppendLine(_localizer["Worker.AdminWordsByLanguage", TelegramMessageHelper.EscapeHtml(kvp.Key), kvp.Value]);

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task EditDictionary(string id, long chatId, CancellationToken ct)
        {
            if (!Guid.TryParse(id, out var dictId))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.InvalidIdentifier"], ct);
                return;
            }

            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            var dictionary = dictionaries.FirstOrDefault(d => d.Id == dictId);
            if (dictionary == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.DictionaryNotFound"], ct);
                return;
            }

            var words = (await _dictionaryRepo.GetWordsAsync(dictId)).ToList();
            if (!words.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.DictionaryEmpty"], ct);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(_localizer["Worker.DictionaryNameHeader", TelegramMessageHelper.EscapeHtml(dictionary.Name)]);
            foreach (var w in words)
            {
                sb.AppendLine(_localizer["Worker.DictionaryWordEntry", TelegramMessageHelper.EscapeHtml(w.Base_Text)]);
            }

            await _msg.SendText(chatId, sb.ToString(), ct);
        }

        private async Task ShowDictionary(Guid dictId, long chatId, CancellationToken ct)
        {
            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            var dictionary = dictionaries.FirstOrDefault(d => d.Id == dictId);
            if (dictionary == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.DictionaryNotFound"], ct);
                return;
            }

            var words = (await _dictionaryRepo.GetWordsAsync(dictId)).ToList();
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);

            var sb = new StringBuilder();
            var dictName = dictionary.Name == "default" ? _localizer["Worker.DictionaryNameDefault"] : dictionary.Name;
            sb.AppendLine(_localizer["Worker.DictionariesByTopicsHeader"]); // Reuse for header "Словари по темам" / or create specific like "DictionaryDetailsHeader"
            sb.Replace("Словари по темам", TelegramMessageHelper.EscapeHtml(dictName)); // Simple replace for now

            foreach (var w in words)
            {
                var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                var right = tr?.Text ?? "-";
                sb.AppendLine(_localizer["Worker.DictionaryWordTranslationEntry", TelegramMessageHelper.EscapeHtml(w.Base_Text), TelegramMessageHelper.EscapeHtml(right)]);
            }

            var actions = _keyboardFactory.GetTopicDictionaryActions(dictId);
            await _msg.SendText(chatId, sb.ToString(), actions, ct);
        }

        private async Task ResetDictionaryProgress(string id, long chatId, CancellationToken ct)
        {
            if (!Guid.TryParse(id, out var dictId))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.InvalidIdentifier"], ct);
                return;
            }

            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var words = (await _dictionaryRepo.GetWordsAsync(dictId)).ToList();
            if (!words.Any())
            {
                await _msg.SendInfoAsync(chatId, _localizer["Worker.NoWordsInDictionary"], ct);
                return;
            }

            foreach (var w in words)
            {
                var prog = await _progressRepo.GetAsync(user.Id, w.Id);
                if (prog == null) continue;
                prog.Repetition = 0;
                prog.Interval_Hours = 0;
                prog.Ease_Factor = 2.5;
                prog.Next_Review = DateTime.UtcNow; // Corrected: prog.Next_Review
                await _progressRepo.InsertOrUpdateAsync(prog);
            }

            await _msg.SendSuccessAsync(chatId, _localizer["Worker.DictionaryProgressReset"], ct);
        }

        private async Task DeleteDictionary(string id, long chatId, CancellationToken ct)
        {
            if (id.StartsWith("confirm_"))
            {
                var strId = id.Substring("confirm_".Length);
                if (!Guid.TryParse(strId, out var dId)) return;
                await PerformDictionaryDeletion(dId, chatId, false, ct);
                return;
            }

            if (!Guid.TryParse(id, out var dictId))
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.InvalidIdentifier"], ct);
                return;
            }

            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.UserNotFound"], ct);
                return;
            }

            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            var dictionary = dictionaries.FirstOrDefault(d => d.Id == dictId);
            if (dictionary == null)
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.DictionaryNotFound"], ct);
                return;
            }

            if (dictionary.Name == "default")
            {
                await _msg.SendErrorAsync(chatId, _localizer["Worker.CannotDeleteDefaultDictionary"], ct);
                return;
            }

            _pendingDeleteDict[chatId] = dictId;

            var kb = new InlineKeyboardMarkup(new[]
            {
                new[]{ InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonYes"], $"delete_dict:confirm_{dictId}") },
                new[]{ InlineKeyboardButton.WithCallbackData(_localizer["Worker.ButtonNo"], _localizer["Worker.ButtonCancel"]) }
            });
            await _msg.SendText(chatId, _localizer["Worker.ConfirmDictionaryDeletionMoveToDefault"], kb, ct);
        }

        private async Task PerformDictionaryDeletion(Guid dictId, long chatId, bool deleteWords, CancellationToken ct)
        {
            var user = await _userRepo.GetByTelegramIdAsync(chatId);
            if (user == null) return;

            var dictionaries = await _dictionaryRepo.GetByUserAsync(user.Id);
            var dictionary = dictionaries.FirstOrDefault(d => d.Id == dictId);
            if (dictionary == null) return;

            var words = (await _dictionaryRepo.GetWordsAsync(dictId)).ToList();
            if (deleteWords)
            {
                foreach (var w in words)
                {
                    await _userWordRepo.RemoveUserWordAsync(user.Id, w.Id); //TODO make mass removal method in UserWordRepository
                }
            }else
            {
                var defaultDict = await _dictionaryRepo.GetDefaultDictionary(user.Id);
                    foreach (var w in words)
                    {
                        await _dictionaryRepo.AddWordAsync(defaultDict.Id, w.Id);
                    }
            }
            

            await _dictionaryRepo.DeleteAsync(dictId);
            _pendingDeleteDict.Remove(chatId);
            await _msg.SendSuccessAsync(chatId, _localizer["Worker.DictionaryDeleted"], ct);
        }

        private async Task ReminderLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now;
                    if (_reminderHours.Contains(now.Hour) &&
                        (_lastReminder.Date != now.Date || _lastReminder.Hour != now.Hour))
                    {
                        await NotifyUsersWithDueWordsAsync(ct);
                        _lastReminder = now;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Reminder loop failed");
                }

                await Task.Delay(_reminderCheckInterval, ct);
            }
        }

        private async Task NotifyUsersWithDueWordsAsync(CancellationToken ct)
        {
            var users = await _userRepo.GetAllUsersAsync();

            foreach (var user in users)
            {
                if (user.Receive_Reminders && await HasDueWordsAsync(user))
                {
                    await StartLearningAsync(user, ct);
                }
            }
        }

        private async Task<bool> HasDueWordsAsync(User user)
        {
            var currentLang = await _languageRepo.GetByNameAsync(user.Current_Language!);
            var all = await _userWordRepo.GetWordsByUserId(user.Id);
            all = all.Where(w => w.Language_Id == currentLang.Id);
            foreach (var w in all)
            {
                var prog = await _progressRepo.GetAsync(user.Id, w.Id);
                if (prog == null || prog.Next_Review <= DateTime.UtcNow)
                    return true;
            }
            return false;
        }

        private Task ShowHelpInformation(long chatId, CancellationToken ct)
        {
            var help = new StringBuilder();
            help.AppendLine(_localizer["Worker.HelpHeader"]);
            help.AppendLine(_localizer["Worker.HelpAddWord"]);
            help.AppendLine(_localizer["Worker.HelpMyWords"]);
            help.AppendLine(_localizer["Worker.HelpLearn"]);
            help.AppendLine();
            help.AppendLine(_localizer["Worker.HelpUseMenu"]);
            return _msg.SendText(chatId, help.ToString(), ct);
        }
    }
}


