using System.Text;
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
        private readonly Dictionary<long, TranslatedTextClass> _translationCandidates = new();
        private readonly Dictionary<long, List<int>> _selectedTranslations = new();
        private readonly Dictionary<long, List<int>> _selectedExamples = new();
        private readonly Dictionary<long, string> _pendingOriginalText = new();
        private readonly Dictionary<long, bool> _originalIsNative = new();
        private readonly SpacedRepetitionService _sr;
        // Для режима редактирования:
        private readonly Dictionary<long, Guid> _pendingEditWordId = new();
        private readonly Dictionary<long, TranslatedTextClass> _editTranslationCandidates = new();
        private readonly Dictionary<long, List<int>> _selectedEditTranslations = new();
        private readonly Dictionary<long, List<int>> _selectedEditExamples = new();

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
            ITelegramBotClient botClient,
            SpacedRepetitionService sr)
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
            _sr = sr;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _botClient.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() },
                cancellationToken: stoppingToken);

            var me = await _botClient.GetMe();
            _logger.LogInformation($"Bot {me.Username} started");
            await Task.Delay(-1, stoppingToken);
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
        {
            if (update.CallbackQuery is { } callback)
            {
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
                Models.User? user = await _userRepo.GetByTelegramIdAsync(userTelegramId);
                var isNewUser = await IsNewUser(user, message);
                if (isNewUser)
                    user = await _userRepo.GetByTelegramIdAsync(userTelegramId);

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
                    }
                    return;
                }

                // Ensure languages are set
                if (string.IsNullOrWhiteSpace(user.Native_Language))
                {
                    _userStates[userTelegramId] = "awaiting_nativelanguage";
                    await _msg.SendInfoAsync(chatId, "Введите ваш родной язык:", ct);
                    return;
                }

                if (string.IsNullOrWhiteSpace(user.Current_Language))
                {
                    _userStates[userTelegramId] = "awaiting_language";
                    await _msg.SendInfoAsync(chatId, "Какой язык хотите изучать?", ct);
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
                            await _msg.SendErrorAsync(chatId, "Сначала добавьте язык через /addlanguage", ct);
                            return;
                        }
                        _userStates[userTelegramId] = "awaiting_addword";
                        await _msg.SendInfoAsync(chatId, "Введите слово для запоминания:", ct);
                        break;

                    case "/learn":
                        await StartLearningAsync(user, ct);
                        break;

                    case "/config":
                        await KeyboardFactory.ShowConfigMenuAsync(_botClient, chatId, ct);
                        break;

                    case "/addlanguage":
                        var parts = text.Split(' ', 2);
                        if (parts.Length < 2)
                        {
                            _userStates[userTelegramId] = "awaiting_language";
                            await _msg.SendInfoAsync(chatId, "Введите название языка:", ct);
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
                            await _msg.SendErrorAsync(chatId, "Используйте /removelanguage [код]", ct);
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
                            : "Список пуст.";
                        await botClient.SendMessage(chatId, list, cancellationToken: ct);
                        break;

                    case "/mylangs":
                        var my = await _userLangRepository.GetUserLanguageNamesAsync(user.Id);
                        if (!my.Any())
                            await _msg.SendErrorAsync(chatId, "У вас нет добавленных языков.", ct);
                        else
                            await _msg.SendInfoAsync(chatId,
                                "Вы изучаете:\n" + string.Join("\n", my), ct);
                        break;

                    case "/clearalldata":
                        await _msg.SendSuccessAsync(chatId, "Сброс данных...", ct);
                        user.Current_Language = null;
                        await _userRepo.UpdateAsync(user);
                        await _translationRepo.RemoveAllTranslations();
                        await _userLangRepository.RemoveAllUserLanguages();
                        await _userWordRepo.RemoveAllUserWords();
                        await _wordRepo.RemoveAllWords();
                        await _msg.SendSuccessAsync(chatId, "Готово", ct);
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
                            await _msg.SendInfoAsync(chatId, "пример: /removeword слово", ct);
                        else
                        {
                            var ok = await _userWordRepo.RemoveUserWordAsync(user.Id, sw[1].Trim());
                            if (ok)
                                await _msg.SendInfoAsync(chatId, $"Слово '{sw[1]}' удалено", ct);
                            else
                                await _msg.SendInfoAsync(chatId, $"Слово '{sw[1]}' не найдено", ct);
                        }
                        break;

                    case "/mywords":
                        await ShowMyWords(chatId, user, ct);
                        break;

                    default:
                        await _msg.SendErrorAsync(chatId, "Неизвестная команда. Используйте меню или /start.", ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update");
                if (ex.Message.Contains("Translation"))
                {
                    await _msg.SendErrorAsync(chatId, ex.Message, ct);
                }
            }
        }

        private async Task ProcessChangeCurrentLanguage(User user, string text, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        private async Task filterMessages(Message? message)
        {
            if (message == null) return;
            var keybord = KeyboardFactory.GetMainMenu();
            if (keybord.Keyboard.Any(x => x.Any(c => c.Text.Contains(message.Text.Trim()))))
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
                await _msg.SendText(new ChatId(chatId),
                    "❌ У вас нет добавленных языков.",
                    ct);
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
                    await _msg.SendText(new ChatId(chatId),
                        $"{header}\nНет слов.",
                        ct);
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
                    await _msg.SendText(new ChatId(chatId),
                        $"{header}\nИспользуйте кнопки «⬅️» и «➡️» для навигации.",
                        ct);

                    // Первая карточка в слайдере
                    var first = words[0];
                    var firstTr = await _translationRepo.GetTranslationAsync(first.Id, native.Id);
                    await _msg.ShowWordSlider(
                        new ChatId(chatId),
                        langId: lang.Id,
                        currentIndex: 0,
                        totalWords: words.Count,
                        word: first.Base_Text,
                        translation: firstTr?.Text ?? "-",
                        example: firstTr?.Examples ?? null,
                        category: lang.Name,
                        imageUrl: null,
                        ct: ct
                    );
                }
            }
        }


        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
        {
            var error = exception switch
            {
                ApiRequestException apiEx => $"Telegram API Error: {apiEx.Message}",
                _ => exception.ToString()
            };
            _logger.LogError(error);
            return Task.CompletedTask;
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
                case "learn": // learn:rem:wordId or learn:fail:wordId
                    var success = parts[1] == "rem";
                    var wordId = Guid.Parse(parts[2]);
                    await UpdateLearningProgressAsync(user, wordId, success, ct);
                    break;
                case "delete":
                    var wordText = parts[1];
                    var removed = await _userWordRepo.RemoveUserWordAsync(user.Id, wordText);
                    if (removed)
                        await _msg.SendSuccessAsync(chatId, $"Слово '{wordText}' удалено", ct);
                    else
                        await _msg.SendErrorAsync(chatId, $"Слово '{wordText}' не найдено", ct);
                    break;
                case "repeat":
                    var repeatText = parts[1];
                    var w = await _wordRepo.GetByTextAsync(repeatText);
                    if (w != null)
                    {
                        var native = await _languageRepo.GetByNameAsync(user.Native_Language);
                        var tr = await _translationRepo.GetTranslationAsync(w.Id, native.Id);
                        await _msg.SendWordCardAsync(chatId, w.Base_Text, tr?.Text, null, ct);
                    }
                    break;
                case "favorite":
                    var favText = parts[1];
                    await _msg.SendSuccessAsync(chatId, $"Слово '{favText}' добавлено в избранное", ct);
                    break;
                case "set_native":
                    _userStates[userTelegramId] = "awaiting_nativelanguage";
                    await _msg.SendInfoAsync(chatId, "Введите ваш родной язык:", ct);
                    break;
                case "switch_language":
                    await HandleSwitchLanguageCommandAsync(user, chatId, ct);
                    break;
                case "add_foreign":
                    _userStates[userTelegramId] = "awaiting_language";
                    await _msg.SendInfoAsync(chatId, "Введите название языка для изучения:", ct);
                    break;
                case "remove_foreign":
                    var langs = await _userLangRepository.GetUserLanguagesAsync(user.Id);
                    if (!langs.Any())
                        await _msg.SendErrorAsync(chatId, "У вас нет добавленных языков.", ct);
                    else
                    {
                        var list = string.Join("\n", langs.Select(l => $"{l.Code} – {l.Name}"));
                        _userStates[userTelegramId] = "awaiting_remove_foreign";
                        await _msg.SendInfoAsync(chatId, $"Ваши языки:\n{list}\nВведите код для удаления:", ct);
                    }
                    break;
                case "switch_lang":
                    await ProcessSwitchLanguage(callback, chatId, user, parts, ct);
                    break;
                case "prev":
                case "next":
                    await HandleSliderNavigationAsync(callback, user, parts, ct);
                    break;
                case "config_learn":
                    switch (parts[1])
                    {
                        case "main":
                            await KeyboardFactory.ShowLearnConfig(bot, chatId, user, ct);
                            return;
                        case "binary":
                            if (user.Prefer_Multiple_Choice)
                            {
                                user.Prefer_Multiple_Choice = false;
                                await _userRepo.UpdateAsync(user);
                                await _msg.SendSuccessAsync(chatId, "Режим обучения изменён на бинарный", ct);
                            }
                            else
                            {
                                await _msg.SendErrorAsync(chatId, "Вы уже используете бинарный режим обучения", ct);
                            }
                            return;
                        case "multiple":
                            if (!user.Prefer_Multiple_Choice)
                            {
                                user.Prefer_Multiple_Choice = true;
                                await _userRepo.UpdateAsync(user);
                                await _msg.SendSuccessAsync(chatId, "Режим обучения изменён на множественный выбор", ct);
                            }
                            else
                            {
                                await _msg.SendErrorAsync(chatId, "Вы уже используете режим множественного выбора", ct);
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
                    await FinalizeAddWord(user!, ct);
                }
                else
                {
                    int idx = int.Parse(data.Split(':')[1]);
                    var sel = _selectedExamples[chatId];
                    if (sel.Contains(idx)) sel.Remove(idx); else sel.Add(idx);
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

            if (data.StartsWith("mc:"))
            {
                // mc:correct:{wordId} или mc:wrong:{wordId}
                var success = parts[1] == "correct";
                var wordId = Guid.Parse(parts[2]);
                var native_lang = await _languageRepo.GetByNameAsync(user.Native_Language);
                var translation = await _translationRepo.GetTranslationAsync(wordId, native_lang.Id);
                // Обновляем прогресс (SM-2) точно так же, как в бинарном режиме
                await UpdateLearningProgressAsync(user, wordId, success, ct);
                if (success)
                // Отвечаем на callback и сразу показываем следующее слово
                await bot.AnswerCallbackQuery(
                    callbackQueryId: callback.Id,
                    text: "✅ Верно!" + Environment.NewLine + translation.Text,
                    cancellationToken: ct
                );
                else
                {
                    var word = await _wordRepo.GetWordById(wordId);
                    //"❌ Неправильно!"
                    await bot.AnswerCallbackQuery(
                    callbackQueryId: callback.Id,
                    text: "❌ Неправильно!",
                    cancellationToken: ct
                );
                    _msg.SendWordCard(chatId, word.Base_Text, translation.Text, translation.Examples, null, null, ct);
                }
                    await SendNextLearningWordAsync(user, chatId, ct);
                return;
            }

            await bot.AnswerCallbackQuery(callback.Id);
        }

        /// <summary>
        /// Сохраняет в БД новый Word + выбранные Translations + Examples.
        /// </summary>
        private async Task FinalizeAddWord(User user, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            if (!_pendingOriginalText.TryGetValue(chatId, out var originalText))
            {
                await _msg.SendErrorAsync(chatId, "Не найдено слово для сохранения", ct);
                return;
            }
            bool isNative = _originalIsNative[chatId];
            var aiResult = _translationCandidates[chatId];

            // Собираем выбранные
            var variants = aiResult.TranslatedText!
                           .Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim()).ToList();
            var chosenVariants = _selectedTranslations[chatId].Select(i => variants[i]).ToList();
            var chosenExamples = (aiResult.Examples ?? new())[..]
                                 .Where((_, i) => _selectedExamples[chatId].Contains(i)).ToList();

            // Чистим
            _pendingOriginalText.Remove(chatId);
            _originalIsNative.Remove(chatId);
            _translationCandidates.Remove(chatId);
            _selectedTranslations.Remove(chatId);
            _selectedExamples.Remove(chatId);

            // Сохраняем
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            Word word;
            Guid firstTransId = Guid.Empty;

            if (isNative)
            {
                // input был на родном: создаём Word из AI-вариантов foreign, а в translations — originalText
                var baseText = chosenVariants.First();
                word = new() { Id = Guid.NewGuid(), Base_Text = baseText, Language_Id = current!.Id };
                await _wordRepo.AddWordAsync(word);
                var tr = new Translation
                {
                    Id = Guid.NewGuid(),
                    Word_Id = word.Id,
                    Language_Id = native!.Id,
                    Text = originalText,
                    Examples = chosenExamples.Any() ? string.Join("\n", chosenExamples) : null
                };
                await _translationRepo.AddTranslationAsync(tr);
                firstTransId = tr.Id;
            }
            else
            {
                // input был foreign: Word = originalText, Translations = chosenVariants
                word = new() { Id = Guid.NewGuid(), Base_Text = originalText, Language_Id = current!.Id };
                await _wordRepo.AddWordAsync(word);
                foreach (var txt in chosenVariants)
                {
                    var tr = new Translation
                    {
                        Id = Guid.NewGuid(),
                        Word_Id = word.Id,
                        Language_Id = native!.Id,
                        Text = txt,
                        Examples = chosenExamples.Any() ? string.Join("\n", chosenExamples) : null
                    };
                    await _translationRepo.AddTranslationAsync(tr);
                    if (firstTransId == Guid.Empty) firstTransId = tr.Id;
                }
            }

            await _userWordRepo.AddUserWordAsync(user.Id, word.Id);
            if (firstTransId != Guid.Empty)
                await _userWordRepo.UpdateTranslationIdAsync(user.Id, word.Id, firstTransId);

            await _msg.SendSuccessAsync(chatId, $"Добавлено «{word.Base_Text}»", ct);
            var displayTrans = isNative ? originalText : chosenVariants.FirstOrDefault() ?? string.Empty;
            var displayEx = chosenExamples.Any() ? string.Join("\n", chosenExamples) : null;
            await _msg.SendWordCard(
                chatId: new ChatId(chatId),
                word: word.Base_Text,
                translation: displayTrans,
                example: displayEx,
                category: current!.Name,
                imageUrl: null,
                ct: ct
            );
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
                await _msg.SendErrorAsync(chatId, "Ошибка при загрузке слова", ct);
                return;
            }

            // AI перевод baseText→native
            var aiResult = await _ai.TranslateWordAsync(word.Base_Text, current.Name, native.Name);
            if (aiResult == null || !aiResult.IsSuccess())
            {
                await _msg.SendErrorAsync(chatId, "Ошибка AI-перевода", ct);
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
            var variants = aiResult.TranslatedText?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList()
              ?? new List<string>();
            var rows = variants
                .Select((t, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedEditTranslations[chatId].Contains(i) ? "✅ " : string.Empty) +
                      TelegramMessageHelper.EscapeHtml(t),
                callbackData: $"editSelectTrans:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "editSelectTransDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Редактируйте переводы:",
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
            var examples = aiResult.Examples ?? new List<string>();
            var rows = examples
                .Select((ex, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedEditExamples[chatId].Contains(i) ? "✅ " : string.Empty) +
                      TelegramMessageHelper.EscapeHtml(ex),
                callbackData: $"editSelectEx:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "editSelectExDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Редактируйте примеры:",
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
            var aiResult = _editTranslationCandidates[chatId];
            var variants = aiResult.TranslatedText!
                           .Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => s.Trim()).ToList();
            var chosenTrans = _selectedEditTranslations[chatId].Select(i => variants[i]).ToList();
            var chosenEx = (aiResult.Examples ?? new())[..]
                              .Where((_, i) => _selectedEditExamples[chatId].Contains(i)).ToList();

            // Чистим
            _pendingEditWordId.Remove(chatId);
            _editTranslationCandidates.Remove(chatId);
            _selectedEditTranslations.Remove(chatId);
            _selectedEditExamples.Remove(chatId);

            // Удаляем все старые
            await _translationRepo.RemoveByWordIdAsync(wordId);

            // Добавляем новые
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            Guid firstTransId = Guid.Empty;
            foreach (var txt in chosenTrans)
            {
                var tr = new Translation
                {
                    Id = Guid.NewGuid(),
                    Word_Id = wordId,
                    Language_Id = native!.Id,
                    Text = txt,
                    Examples = chosenEx.Any() ? string.Join("\n", chosenEx) : null
                };
                await _translationRepo.AddTranslationAsync(tr);
                if (firstTransId == Guid.Empty) firstTransId = tr.Id;
            }

            // Обновляем UserWord.translation_id
            if (firstTransId != Guid.Empty)
                await _userWordRepo.UpdateTranslationIdAsync(user.Id, wordId, firstTransId);

            // Отправляем карточку
            var word = await _wordRepo.GetWordById(wordId);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            await _msg.SendSuccessAsync(chatId, $"Обновлено «{word!.Base_Text}»", ct);
            await _msg.SendWordCard(
                chatId: new ChatId(chatId),
                word: word.Base_Text,
                translation: chosenTrans.FirstOrDefault() ?? string.Empty,
                example: chosenEx.Any() ? string.Join("\n", chosenEx) : null,
                category: current!.Name,
                imageUrl: null,
                ct: ct
            );
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

            await _msg.ShowWordSlider(
                new ChatId(chatId),
                langId: langId,
                currentIndex: newIndex,
                totalWords: words.Count,
                word: word.Base_Text,
                translation: tr?.Text,
                example: tr?.Examples,
                category: category,
                imageUrl: null,
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
                    text: "Неверный идентификатор языка.",
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
                    text: "Этот язык не найден в вашем списке.",
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
                text: $"Текущий язык переключён на «{userLangs.First(lg => lg.Id == newLangId).Name}».",
                cancellationToken: ct
            );

            await _botClient.AnswerCallbackQuery(
                callbackQueryId: callback.Id,
                text: "Язык успешно изменён.",
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
                await _msg.SendErrorAsync(chatId, "У вас ещё нет ни одного изучаемого языка.", ct);
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
                text: "Выберите язык, который хотите сделать текущим:",
                replyMarkup: keyboard,
                cancellationToken: ct
            );
        }

        private async Task<(bool handled, string newState)> HandleKeyboardCommandAsync(User user, string command, long chatId, CancellationToken ct)
        {
            switch (command.ToLowerInvariant())
            {
                case "📚 мои слова":
                    await ShowMyWords(chatId, user, ct);
                    return (true, string.Empty);

                case "➕ добавить слово":
                    //await _botClient.DeleteMessage(chatId,);
                    await _msg.SendInfoAsync(chatId, "Введите слово для добавления:", ct);
                    return (true, "awaiting_addword");

                case "📖 учить":
                    await StartLearningAsync(user, ct);
                    return (true, string.Empty);

                case "⚙️ настройки":
                    await KeyboardFactory.ShowConfigMenuAsync(_botClient, chatId, ct);
                    return (true, string.Empty);

                case "📊 статистика":
                    await ShowStatisticsAsync(user, chatId, ct);
                    return (true, string.Empty);

                case "❓ помощь":
                    await _botClient.SendMessage(
                        chatId,
                        "Я бот для изучения слов. Используй меню или команды: /addword, /learn, /config",
                        cancellationToken: ct);
                    return (true, string.Empty);

                default:
                    return (false, string.Empty);
            }
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

            //отправка карточки и переход к next
           // await SendNextLearningWordAsync(user, user.Telegram_Id, ct);
        }

        private async Task SendNextLearningWordAsync(User user, long chatId, CancellationToken ct)
        {
            var all = await _userWordRepo.GetWordsByUserId(user.Id);
            var due = new List<Word>();

            foreach (var w in all)
            {
                var progress = await _progressRepo.GetAsync(user.Id, w.Id);
                if (progress?.Next_Review <= DateTime.UtcNow)
                {
                    due.Add(w);
                }
            }

            if (!due.Any())
            {
                await _msg.SendInfoAsync(chatId, "Нечего повторять.", ct);
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
            if (word_native == null) throw new Exception("GetTranslationAsync = null, ShowMultipleChoiceAsync");
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
            var keyboard = new InlineKeyboardMarkup(buttons.Chunk(2));

            // Отправляем сообщение
            await _msg.SendText(
                chatId: user.Telegram_Id,
                text: $"Выберите правильный перевод для слова «{word.Base_Text}»:",
                replyMarkup: keyboard,
                ct: ct
            );
        }

        private async Task ShowBinaryChoiceAsync(long chatId, Word word, CancellationToken ct)
        {
            var inline = new InlineKeyboardMarkup(new[]
                        {
                new[] { InlineKeyboardButton.WithCallbackData("✅ Вспомнил", $"learn:rem:{word.Id}") },
                new[] { InlineKeyboardButton.WithCallbackData("❌ Не вспомнил", $"learn:fail:{word.Id}") }
            });

            await _botClient.SendMessage(chatId, $"Переведите слово: <b>{word.Base_Text}</b>", parseMode: ParseMode.Html, replyMarkup: inline, cancellationToken: ct);
        }


        private async Task ProcessRemoveForeignLanguage(User user, string code, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var lang = await _languageRepo.GetByCodeAsync(code);
            if (lang == null) { await _msg.SendErrorAsync(chatId, $"Язык {code} не найден", ct); return; }
            await _userLangRepository.RemoveUserLanguageAsync(user.Id, lang.Id);
            await _msg.SendSuccessAsync(chatId, $"Язык {lang.Name} удалён", ct);
        }

        private async Task ProcessAddWord(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var current = await _languageRepo.GetByNameAsync(user.Current_Language!);
            if (native == null || current == null)
            {
                await _msg.SendErrorAsync(chatId, "Языки не настроены", ct);
                return;
            }

            // 1) Определяем язык ввода
            var inputLangName = await _ai.GetLangName(text);
            if (string.IsNullOrWhiteSpace(inputLangName) || inputLangName.ToLower() == "error")
            {
                await _msg.SendErrorAsync(chatId, "Не удалось определить язык слова", ct);
                return;
            }
            var inputLang = await _languageRepo.GetByNameAsync(inputLangName);
            if (inputLang == null)
            {
                await _msg.SendErrorAsync(chatId, $"Язык '{inputLangName}' не в базе", ct);
                return;
            }
            bool isNativeInput = inputLang.Id == native.Id;

            // 2) Проверяем, нет ли уже этого слова/перевода в базе
            if (!isNativeInput)
            {
                // Пользователь ввёл слово на изучаемом языке
                var existingWord = await _wordRepo.GetByTextAndLanguageAsync(text, current.Id);
                if (existingWord != null)
                {
                    // Слово есть в базе — проверим у пользователя
                    var has = await _userWordRepo.UserHasWordAsync(user.Id, existingWord.Base_Text);
                    if (has)
                    {
                        await _msg.SendInfoAsync(chatId, $"«{text}» уже есть в вашем словаре.", ct);
                    }
                    else
                    {
                        // Привязываем к пользователю
                        await _userWordRepo.AddUserWordAsync(user.Id, existingWord.Id);
                        await _msg.SendSuccessAsync(chatId, $"«{text}» добавлено в ваш словарь.", ct);
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
                    if (foreignWord != null)
                    {
                        var has = await _userWordRepo.UserHasWordAsync(user.Id, foreignWord.Base_Text);
                        if (has)
                        {
                            await _msg.SendInfoAsync(chatId, $"«{foreignWord.Base_Text}» уже есть в вашем словаре.", ct);
                        }
                        else
                        {
                            await _userWordRepo.AddUserWordAsync(user.Id, foreignWord.Id);
                            await _msg.SendSuccessAsync(chatId, $"«{foreignWord.Base_Text}» добавлено в ваш словарь.", ct);
                        }
                        return;
                    }
                }
            }

            // 3) Иначе — запускаем AI-перевод
            var aiResult = isNativeInput
                ? await _ai.TranslateWordAsync(text, native.Name, current.Name)
                : await _ai.TranslateWordAsync(text, current.Name, native.Name);
            if (aiResult == null || !aiResult.IsSuccess())
            {
                await _msg.SendErrorAsync(chatId, "Ошибка AI-перевода", ct);
                return;
            }
            var variants = aiResult.TranslatedText
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(s => s.Trim())
        .ToList();
            var examples = aiResult.Examples ?? new();
            if (variants.Count == 1 && (examples.Count <= 1))
            {
                // Готовим выбранные индексы
                _pendingOriginalText[chatId] = text;
                _originalIsNative[chatId] = isNativeInput;
                _translationCandidates[chatId] = aiResult;
                _selectedTranslations[chatId] = new List<int> { 0 };
                _selectedExamples[chatId] = examples.Count == 1
                                                 ? new List<int> { 0 }
                                                 : new List<int>();

                await FinalizeAddWord(user, ct);
                return;
            }

            // Сохраняем временные данные и сразу отмечаем первый вариант
            _pendingOriginalText[chatId] = text;
            _originalIsNative[chatId] = isNativeInput;
            _translationCandidates[chatId] = aiResult;
            _selectedTranslations[chatId] = new List<int> { 0 };
            _selectedExamples[chatId] = new() { 0 };

            // 5) Отдаём выбор переводов
            await ShowTranslationOptions(chatId, aiResult, ct);
        }

        /// <summary>
        /// Показать inline-клавиатуру переводов (для добавления).
        /// </summary>
        private async Task ShowTranslationOptions(long chatId, TranslatedTextClass aiResult, CancellationToken ct)
        {
            var variants = aiResult.TranslatedText?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToList()
              ?? new List<string>();

            var rows = variants
                .Select((t, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedTranslations[chatId].Contains(i) ? "✅ " : string.Empty) +
                      TelegramMessageHelper.EscapeHtml(t),
                callbackData: $"selectTrans:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "selectTransDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Выберите перевод(ы):",
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
            var examples = aiResult.Examples ?? new List<string>();
            var rows = examples
                .Select((ex, i) => new[] {
            InlineKeyboardButton.WithCallbackData(
                text: (_selectedExamples[chatId].Contains(i) ? "✅ " : string.Empty) +
                      TelegramMessageHelper.EscapeHtml(ex),
                callbackData: $"selectEx:{i}"
            )
                }).ToList();
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "selectExDone") });
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Выберите примеры употребления:",
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
                await _msg.SendErrorAsync(chatId, "Не удалось распознать язык", ct);
                return;
            }
            var lang = await _languageRepo.GetByNameAsync(name);
            user.Native_Language = lang!.Name;
            await _userRepo.UpdateAsync(user);
            await _botClient.SendMessage(chatId, $"Родной язык установлен: {lang.Name}", cancellationToken: ct);
        }

        private async Task ProcessAddLanguage(User user, string text, CancellationToken ct)
        {
            var chatId = user.Telegram_Id;
            var name = await _ai.GetLangName(text);
            if (name.ToLowerInvariant() == "error")
            {
                await _msg.SendErrorAsync(chatId, "Не удалось распознать язык", ct);
                return;
            }
            var lang = await _languageRepo.GetByNameAsync(name);
            await _userLangRepository.AddUserLanguageAsync(user.Id, lang!.Id);
            user.Current_Language = lang.Name;
            await _userRepo.UpdateAsync(user);
            await _botClient.SendMessage(chatId,
                $"Язык {lang.Name} добавлен. Выберите слова через /addword или меню", cancellationToken: ct);
        }

        private async Task ProcessStartCommand(User user, Message message, CancellationToken ct)
        {
            var isNew = await IsNewUser(user, message);
            var chatId = message.Chat.Id;
            if (isNew)
            {
                if (isNew) await _msg.SendInfoAsync(chatId, "Привет! Я бот для изучения слов.", ct);
            }
            await KeyboardFactory.ShowMainMenuAsync(_botClient, chatId, ct);
        }

        private async Task<bool> IsNewUser(Models.User? user, Message message)
        {
            if (user == null)
            {
                var lang = await _languageRepo.GetByCodeAsync(message.From!.LanguageCode);
                user = new User { Id = Guid.NewGuid(), Telegram_Id = message.From.Id, Native_Language = lang?.Name ?? string.Empty };
                await _userRepo.AddAsync(user);
                return true;
            }
            return false;
        }

        private async Task<Word> CreateWordWithTranslationAsync(Guid userId, string inputText, Language nativeLang, Language targetLang)
        {
            try
            {
                var langs = (await _userLangRepository.GetUserLanguagesAsync(userId)).ToList();
                langs.Add(nativeLang);
                var inputTextLanguage = await _ai.GetLangName(inputText, langs);
                if (string.IsNullOrWhiteSpace(inputTextLanguage) || inputTextLanguage.ToLower() == "error")
                {
                    throw new Exception("Translation. Не удалось определить язык текста: " + inputText);
                }

                Language inputLanguage = await _languageRepo.GetByNameAsync(inputTextLanguage);
                if (inputLanguage == null) throw new Exception($"Translation. Не удалось найти язык {inputTextLanguage} в базе");

                Guid translationId;
                string translationText = string.Empty;
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
                                throw new Exception("Translation. Ошибка получения перевода AI");
                            }

                            var newGenTrans = new Translation
                            {
                                Id = Guid.NewGuid(),
                                Word_Id = word.Id,
                                Language_Id = nativeLang.Id,
                                Text = translationText,
                                Examples = aiTranslation.GetExampleString() ?? string.Empty
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
                            Id = Guid.NewGuid(),
                            Base_Text = inputText,
                            Language_Id = targetLang.Id
                        };

                        //переводим
                        var translation = await _ai.TranslateWordAsync(inputText, targetLang.Name, nativeLang.Name);
                        if (translation == null || !translation.IsSuccess() || string.IsNullOrEmpty(translation.TranslatedText))
                        {
                            throw new Exception("Translation. Ошибка получения перевода AI");
                        }

                        Translation wordTranslation = new Translation
                        {
                            Id = Guid.NewGuid(),
                            Word_Id = newWord.Id,
                            Language_Id = nativeLang.Id,
                            Text = translation.TranslatedText,
                            Examples = translation.GetExampleString() ?? string.Empty
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
                                if (translToForeign == null || !translToForeign.IsSuccess() || string.IsNullOrEmpty(translToForeign.TranslatedText))
                                {
                                    throw new Exception("Translation. Ошибка получения перевода AI");
                                }
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
                        throw new Exception("Translation. Ошибка получения перевода AI");
                    }

                    Word newWord = new Word
                    {
                        Id = Guid.NewGuid(),
                        Base_Text = translation.TranslatedText,
                        Language_Id = targetLang.Id
                    };

                    Translation wordTranslation = new()
                    {
                        Id = Guid.NewGuid(),
                        Word_Id = newWord.Id,
                        Language_Id = nativeLang.Id,
                        Text = inputText,
                        Examples = translation.GetExampleString() ?? string.Empty
                    };
                    await _wordRepo.AddWordAsync(newWord);
                    await _translationRepo.AddTranslationAsync(wordTranslation);
                    await _userWordRepo.AddUserWordAsync(userId, newWord.Id);
                    return newWord;
                }
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);
            }
        }

        private async Task ShowStatisticsAsync(User user, ChatId chatId, CancellationToken ct)
        {
            // Получаем список изучаемых языков
            var langs = (await _userLangRepository.GetUserLanguagesAsync(user.Id)).ToList();
            if (!langs.Any())
            {
                await _msg.SendText(chatId, "❌ У вас нет добавленных языков.", ct);
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("📊 <b>Статистика изучения</b>");
            sb.AppendLine();

            int grandTotalWords = 0, grandTotalViews = 0, grandTotalPlus = 0, grandTotalMinus = 0;

            // Для каждого языка собираем и выводим статистику
            foreach (var lang in langs)
            {
                // Слова пользователя в этом языке
                var words = (await _userWordRepo.GetWordsByUserId(user.Id, lang.Id)).ToList();
                int countWords = words.Count;
                grandTotalWords += countWords;

                // Собираем прогресс для каждого слова
                int sumViews = 0, sumPlus = 0, sumMinus = 0;
                foreach (var w in words)
                {
                    var prog = await _progressRepo.GetAsync(user.Id, w.Id);
                    if (prog != null)
                    {
                        sumViews += prog.Count_Total_View;
                        sumPlus += prog.Count_Plus;
                        sumMinus += prog.Count_Minus;
                    }
                }

                grandTotalViews += sumViews;
                grandTotalPlus += sumPlus;
                grandTotalMinus += sumMinus;

                // Вычисляем процент успеха
                string successRate = (sumPlus + sumMinus) > 0
                    ? $"{Math.Round(sumPlus * 100.0 / (sumPlus + sumMinus))}%"
                    : "–";

                sb
                    .AppendLine($"<b>{TelegramMessageHelper.EscapeHtml(lang.Name)}</b> ({countWords}):")
                    .AppendLine($"  👁 Просмотров: {sumViews}")
                    .AppendLine($"  ✅ Успешных:   {sumPlus}")
                    .AppendLine($"  ❌ Неуспешных: {sumMinus}")
                    .AppendLine($"  🏆 Успех:       {successRate}")
                    .AppendLine();
            }

            // Итоговые цифры
            string grandRate = (grandTotalPlus + grandTotalMinus) > 0
                ? $"{Math.Round(grandTotalPlus * 100.0 / (grandTotalPlus + grandTotalMinus))}%"
                : "–";

            sb
                .AppendLine("<b>Всего по всем языкам:</b>")
                .AppendLine($"  🗂 Слов:       {grandTotalWords}")
                .AppendLine($"  👁 Просмотров: {grandTotalViews}")
                .AppendLine($"  ✅ Успешных:   {grandTotalPlus}")
                .AppendLine($"  ❌ Неуспешных: {grandTotalMinus}")
                .AppendLine($"  🏆 Успех:       {grandRate}");

            // Отправляем одним сообщением
            await _msg.SendText(chatId, sb.ToString(), ct);
        }
    }
}


