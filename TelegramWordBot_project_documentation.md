#======Структура проекта:=====
 
[ROOT] TelegramWordBot
├──  Db.cs
├──  Dockerfile
├──  Program.cs
├──  Worker.cs
├──  Models
│        ├──  Language.cs
│        ├──  Translation.cs
│        ├──  User.cs
│        ├──  UserWord.cs
│        ├──  UserWordProgress.cs
│        └──  Word.cs
├──  Properties
│        ├──  Resources.Designer.cs
│        └──  Resources.resx
├──  Repositories
│        ├──  LanguageRepository.cs
│        ├──  TranslationRepository.cs
│        ├──  UserLanguageRepository.cs
│        ├──  UserRepository.cs
│        ├──  UserWordProgressRepository.cs
│        ├──  UserWordRepository.cs
│        └──  WordRepository.cs
└──  Services
        ├──  AIHelper.cs
        ├──  GeminiAPI.cs
        ├──  KeyboardFactory.cs
        ├──  TelegramMessageHelper.cs
        └──  TranslatedTextClass.cs
===============================

# Содержимое файлов

## Файл: ..\..\..\..\TelegramWordBot\Db.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Npgsql;

namespace TelegramWordBot
{
    public interface IConnectionFactory
    {
        IDbConnection CreateConnection();
    }

    public class DbConnectionFactory : IConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
            => new NpgsqlConnection(_connectionString);

    public static string ConvertDatabaseUrl(string databaseUrl)
        {
            var uri = new Uri(databaseUrl);
            var userInfo = uri.UserInfo.Split(':');
            return $"Host={uri.Host};Port={uri.Port};Username={userInfo[0]};Password={userInfo[1]};Database={uri.AbsolutePath.TrimStart('/')};SSL Mode=Require;Trust Server Certificate=true";
        }

    }



    
}

```

## Файл: ..\..\..\..\TelegramWordBot\Dockerfile
```
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TelegramWordBot.dll"]
```

## Файл: ..\..\..\..\TelegramWordBot\Program.cs
```csharp
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
builder.Services.AddHttpClient();



builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

```

## Файл: ..\..\..\..\TelegramWordBot\Worker.cs
```csharp
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramWordBot.Repositories;
using TelegramWordBot.Models;
using TelegramWordBot.Services;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using User = TelegramWordBot.Models.User;
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

        private async Task  filterMessages(Message? message)
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

                if (words.Count <= 5)
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
                        example: firstTr?.Examples?? null,
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
            }
            await bot.AnswerCallbackQuery(callback.Id);
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
            var category = lang?.Name ?? "";

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
            var chatId = user.Telegram_Id;
            var prog = await _progressRepo.GetAsync(user.Id, wordId) ?? new UserWordProgress { User_Id = user.Id, Word_Id = wordId };
            prog.Count_Total_View++;
            if (success) prog.Count_Plus++;
            else prog.Count_Minus++;
            prog.Progress += success ? 10 : -5;
            prog.Last_Review = DateTime.UtcNow;
            await _progressRepo.InsertOrUpdateAsync(prog, success);

            var native = await _languageRepo.GetByNameAsync(user.Native_Language);
            var tr = await _translationRepo.GetTranslationAsync(wordId, native.Id);
            var word = await _wordRepo.GetWordById(wordId);
            if (word == null)
            {
                await _msg.SendErrorAsync(chatId, "Слово не найдено", ct);
                return;
            }
            string wordCard = _msg.GenerateWordCardText(word.Base_Text, tr?.Text?? "", tr?.Examples ?? "", null);
            if (!success)
            {
                await _msg.SendErrorAsync(chatId, wordCard, ct);
            }
            else
            {
                await _msg.SendSuccessAsync(chatId, wordCard, ct);
            }
            await SendNextLearningWordAsync(user, chatId, ct);
        }

        private async Task SendNextLearningWordAsync(User user, long chatId, CancellationToken ct)
        {
            var all = await _userWordRepo.GetWordsByUserId(user.Id);
            var due = new List<Word>();
            var now = DateTime.UtcNow;
            foreach (var w in all)
            {
                var p = await _progressRepo.GetAsync(user.Id, w.Id);
                var next = p == null ? DateTime.MinValue : p.Last_Review!.Value.AddDays(p.Progress / 10);
                if (p == null || next <= now) due.Add(w);
            }
            if (!due.Any())
            {
                await _msg.SendInfoAsync(chatId, "Нечего повторять.", ct);
                return;
            }
            var rnd = new Random();
            var word = due[rnd.Next(due.Count)];
            //await _msg.SendInfoAsync(chatId, $"Переведите слово: <b>{word.Base_Text}</b>", ct);
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
            if (current == null || native == null)
            {
                await _msg.SendErrorAsync(chatId, "Языки не настроены", ct);
                return;
            }
            var exists = await _userWordRepo.UserHasWordAsync(user.Id, text);
            if (exists)
            {
                await _msg.SendInfoAsync(chatId, $"'{text}' уже есть в списке", ct);
                return;
            }
            var word = await CreateWordWithTranslationAsync(user.Id, text, native, current);
            var tr = await _translationRepo.GetTranslationAsync(word.Id, native.Id);
            await _msg.SendSuccessAsync(chatId, $"Добавлено {word.Base_Text}", ct);
            await _msg.SendWordCardAsync(chatId, word.Base_Text, tr!.Text, null, ct);
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
                                throw new Exception("Translation. Ошибка получения перевода AI");
                            }

                            var newGenTrans = new Translation
                            {
                                Id = Guid.NewGuid(),
                                Word_Id = word.Id,
                                Language_Id = nativeLang.Id,
                                Text = translationText,
                                Examples = aiTranslation.GetExampleString() ?? ""
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
                            Examples = translation.GetExampleString() ?? ""
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
                        Examples = translation.GetExampleString()?? ""
                    };
                    await _wordRepo.AddWordAsync(newWord);
                    await _translationRepo.AddTranslationAsync(wordTranslation);
                    await _userWordRepo.AddUserWordAsync(userId, newWord.Id);
                    return newWord;
                }
            }
            catch (Exception ex)            {
                
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



```

## Файл: ..\..\..\..\TelegramWordBot\Models\Language.cs
```csharp
namespace TelegramWordBot.Models;

public class Language
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty; // Например, "en"
    public string Name { get; set; } = string.Empty; // Например, "English"
}

```

## Файл: ..\..\..\..\TelegramWordBot\Models\Translation.cs
```csharp

namespace TelegramWordBot.Models
{
 public  class Translation
    {
        public Guid Id { get; set; }
        public Guid Word_Id { get; set; }
        public int Language_Id { get; set; }
        public string Text { get; set; }
        public string? Examples { get; set; }

    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Models\User.cs
```csharp
namespace TelegramWordBot.Models;

public class User
{
    public Guid Id { get; set; }
    public long Telegram_Id { get; set; }
    public string Native_Language { get; set; } = "Russian";
    public string? Current_Language { get; set; }

}

```

## Файл: ..\..\..\..\TelegramWordBot\Models\UserWord.cs
```csharp
namespace TelegramWordBot.Models;

public class UserWord
{
    public Guid User_Id { get; set; }
    public Guid Word_Id { get; set; }
    public Guid? Translation_Id { get; set; }
    

}

```

## Файл: ..\..\..\..\TelegramWordBot\Models\UserWordProgress.cs
```csharp
namespace TelegramWordBot.Models;

public class UserWordProgress
{
    public Guid Id { get; set; }
    public Guid User_Id { get; set; }
    public Guid Word_Id { get; set; }
    public DateTime? Last_Review { get; set; }
    public int Count_Total_View { get; set; } = 0;
    public int Count_Plus { get; set; } = 0;
    public int Count_Minus { get; set; } =0;
    public int Progress { get; set; } = 0;
}

```

## Файл: ..\..\..\..\TelegramWordBot\Models\Word.cs
```csharp

namespace TelegramWordBot.Models
{
    public class Word
    {
        public Guid Id { get; set; }
        public string Base_Text { get; set; }
        public int Language_Id { get; set; }

    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Properties\Resources.Designer.cs
```csharp
//------------------------------------------------------------------------------
// <auto-generated>
//     Этот код создан программой.
//     Исполняемая версия:4.0.30319.42000
//
//     Изменения в этом файле могут привести к неправильной работе и будут потеряны в случае
//     повторной генерации кода.
// </auto-generated>
//------------------------------------------------------------------------------

namespace TelegramWordBot.Properties {
    using System;
    
    
    /// <summary>
    ///   Класс ресурса со строгой типизацией для поиска локализованных строк и т.д.
    /// </summary>
    // Этот класс создан автоматически классом StronglyTypedResourceBuilder
    // с помощью такого средства, как ResGen или Visual Studio.
    // Чтобы добавить или удалить член, измените файл .ResX и снова запустите ResGen
    // с параметром /str или перестройте свой проект VS.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Возвращает кэшированный экземпляр ResourceManager, использованный этим классом.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("TelegramWordBot.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Перезаписывает свойство CurrentUICulture текущего потока для всех
        ///   обращений к ресурсу с помощью этого класса ресурса со строгой типизацией.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Properties\Resources.resx
```
<?xml version="1.0" encoding="utf-8"?>
<root>
	<!-- 
		Microsoft ResX Schema

		Version 1.3

		The primary goals of this format is to allow a simple XML format 
		that is mostly human readable. The generation and parsing of the 
		various data types are done through the TypeConverter classes 
		associated with the data types.

		Example:

		... ado.net/XML headers & schema ...
		<resheader name="resmimetype">text/microsoft-resx</resheader>
		<resheader name="version">1.3</resheader>
		<resheader name="reader">System.Resources.ResXResourceReader, System.Windows.Forms, ...</resheader>
		<resheader name="writer">System.Resources.ResXResourceWriter, System.Windows.Forms, ...</resheader>
		<data name="Name1">this is my long string</data>
		<data name="Color1" type="System.Drawing.Color, System.Drawing">Blue</data>
		<data name="Bitmap1" mimetype="application/x-microsoft.net.object.binary.base64">
			[base64 mime encoded serialized .NET Framework object]
		</data>
		<data name="Icon1" type="System.Drawing.Icon, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64">
			[base64 mime encoded string representing a byte array form of the .NET Framework object]
		</data>

		There are any number of "resheader" rows that contain simple 
		name/value pairs.

		Each data row contains a name, and value. The row also contains a 
		type or mimetype. Type corresponds to a .NET class that support 
		text/value conversion through the TypeConverter architecture. 
		Classes that don't support this are serialized and stored with the 
		mimetype set.

		The mimetype is used for serialized objects, and tells the 
		ResXResourceReader how to depersist the object. This is currently not 
		extensible. For a given mimetype the value must be set accordingly:

		Note - application/x-microsoft.net.object.binary.base64 is the format 
		that the ResXResourceWriter will generate, however the reader can 
		read any of the formats listed below.

		mimetype: application/x-microsoft.net.object.binary.base64
		value   : The object must be serialized with 
			: System.Serialization.Formatters.Binary.BinaryFormatter
			: and then encoded with base64 encoding.

		mimetype: application/x-microsoft.net.object.soap.base64
		value   : The object must be serialized with 
			: System.Runtime.Serialization.Formatters.Soap.SoapFormatter
			: and then encoded with base64 encoding.

		mimetype: application/x-microsoft.net.object.bytearray.base64
		value   : The object must be serialized into a byte array 
			: using a System.ComponentModel.TypeConverter
			: and then encoded with base64 encoding.
	-->
	
	<xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
		<xsd:element name="root" msdata:IsDataSet="true">
			<xsd:complexType>
				<xsd:choice maxOccurs="unbounded">
					<xsd:element name="data">
						<xsd:complexType>
							<xsd:sequence>
								<xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
								<xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
							</xsd:sequence>
							<xsd:attribute name="name" type="xsd:string" msdata:Ordinal="1" />
							<xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
							<xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
						</xsd:complexType>
					</xsd:element>
					<xsd:element name="resheader">
						<xsd:complexType>
							<xsd:sequence>
								<xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
							</xsd:sequence>
							<xsd:attribute name="name" type="xsd:string" use="required" />
						</xsd:complexType>
					</xsd:element>
				</xsd:choice>
			</xsd:complexType>
		</xsd:element>
	</xsd:schema>
	<resheader name="resmimetype">
		<value>text/microsoft-resx</value>
	</resheader>
	<resheader name="version">
		<value>1.3</value>
	</resheader>
	<resheader name="reader">
		<value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
	<resheader name="writer">
		<value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=2.0.3500.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
	</resheader>
</root>
```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\LanguageRepository.cs
```csharp
using Dapper; 
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories { 
    public class LanguageRepository { 
        private readonly DbConnectionFactory _factory;

    public LanguageRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<Language>> GetAllAsync()
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryAsync<Language>("SELECT * FROM languages ORDER BY name");
    }

    public async Task<Language?> GetByCodeAsync(string? code)
    {
            if (code == null) return null;
            code = code.ToLower();
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<Language>("SELECT * FROM languages WHERE code = @code", new { code });
    }

    public async Task<Language?> GetByNameAsync(string? name)
    {
            if (string.IsNullOrEmpty(name)) return null;

            // ������������ ��������: ������ ����� � ���������, ��������� � ��������
            string normalizedName = string.Create(name.Length, name, (chars, input) =>
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i] = (i == 0)
                        ? char.ToUpperInvariant(input[i])
                        : char.ToLowerInvariant(input[i]);
                }
            });

            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Language>(
                "SELECT * FROM languages WHERE name = @normalizedName",
                new { normalizedName }
            );
        }

    public async Task AddAsync(Language language)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("INSERT INTO languages (code, name) VALUES (@Code, @Name)", language);
    }

    public async Task DeleteAsync(string code)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync("DELETE FROM languages WHERE code = @code", new { code });
    }
}

}


```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\TranslationRepository.cs
```csharp
using System.Transactions;
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class TranslationRepository
{
    private readonly DbConnectionFactory _factory;

    public TranslationRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddTranslationAsync(Translation translation)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            INSERT INTO translations (id, word_id, language_id, text, examples)
            VALUES (@Id, @Word_Id, @Language_Id, @Text, @Examples)";
        await conn.ExecuteAsync(sql, translation);
    }

    public async Task RemoveAllTranslations()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM translations";
        await conn.ExecuteAsync(sql);
    }

    public async Task<IEnumerable<Translation>> GetTranslationsForWordAsync(Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @Word_Id";
        return await conn.QueryAsync<Translation>(sql, new { Word_Id = wordId });
    }

    public async Task<Translation?> GetTranslationAsync(Guid wordId, int targetLangId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE word_id = @Word_Id AND language_id = @Language_Id LIMIT 1";
        return await conn.QueryFirstOrDefaultAsync<Translation>(sql, new { Word_Id = wordId, Language_Id = targetLangId });
    }

    public async Task<bool> ExistTranslate(Guid? wordId, int targetLangId)
    {
        if (wordId == null) return false;
        using var conn = _factory.CreateConnection();
        var sql = @"SELECT EXISTS (
                   SELECT 1 FROM translations
                   WHERE word_id = @Word_Id AND language_id = @Language_Id)";

        return await conn.ExecuteScalarAsync<bool>(sql, new { Word_Id = wordId, Language_Id = targetLangId });
    }

    public async Task<IEnumerable<Translation>> FindWordByText(string text)
    {
        text = text.Trim();
        if (string.IsNullOrEmpty(text)) return new List<Translation>();
        using var conn = _factory.CreateConnection();
        var sql = "SELECT * FROM translations WHERE text = @Text";
        return await conn.QueryAsync<Translation>(sql, new { Text = text });
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\UserLanguageRepository.cs
```csharp
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserLanguageRepository
{
    private readonly DbConnectionFactory _factory;

    public UserLanguageRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AddUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "INSERT INTO user_languages (user_id, language_id) VALUES (@User_Id, @Language_Id) ON CONFLICT DO NOTHING";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Language_Id = languageId });
    }

    public async Task RemoveUserLanguageAsync(Guid userId, int languageId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "DELETE FROM user_languages WHERE user_id = @User_Id AND language_id = @Language_Id";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Language_Id = languageId });
    }

    public async Task<IEnumerable<int>> GetUserLanguageIdsAsync(Guid userId)
    {
        using var conn = _factory.CreateConnection();
        var sql = "SELECT language_id FROM user_languages WHERE user_id = @User_Id";
        return await conn.QueryAsync<int>(sql, new { User_Id = userId });
    }

    public async Task<IEnumerable<string>> GetUserLanguageNamesAsync(Guid userId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
            SELECT l.name 
            FROM user_languages ul
            INNER JOIN languages l ON ul.language_id = l.id
            WHERE ul.user_id = @User_Id";
            return await conn.QueryAsync<string>(sql, new { User_Id = userId });
        }catch
        {
            return new List<string>(); 
        }
    }

    public async Task<IEnumerable<Language>> GetUserLanguagesAsync(Guid userId)
    {
        try
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
    SELECT l.id AS Id, l.code AS Code, l.name AS Name
    FROM user_languages ul
    INNER JOIN languages l ON ul.language_id = l.id
    WHERE ul.user_id = @User_Id";
            return await conn.QueryAsync<Language>(sql, new { User_Id = userId });
        }
        catch
        {
            return new List<Language>();
        }
    }

    public async Task RemoveAllUserLanguages()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_languages";
        await conn.ExecuteAsync(sql);
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\UserRepository.cs
```csharp
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserRepository
{
    private readonly DbConnectionFactory _factory;

    public UserRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        using var conn = _factory.CreateConnection();
        User? user = await conn.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE telegram_id = @Telegram_Id", new { Telegram_Id = telegramId });
        return user;
    }

    public async Task AddAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "INSERT INTO users (id, telegram_id, native_language, current_language) VALUES (@Id, @Telegram_Id, @Native_Language, @Current_Language)", user);
    }

    public async Task UpdateAsync(User user)
    {
        using var conn = _factory.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE users SET telegram_id = @Telegram_Id, native_language = @Native_Language, current_language = @Current_Language WHERE id = @Id", user);
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\UserWordProgressRepository.cs
```csharp
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserWordProgressRepository
{
    private readonly DbConnectionFactory _factory;

    public UserWordProgressRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<UserWordProgress?> GetAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<UserWordProgress>(
            "SELECT * FROM user_word_progress WHERE user_id = @userId AND word_id = @wordId",
            new { userId, wordId });
    }

    public async Task InsertOrUpdateAsync(UserWordProgress progress, bool success)
    {
        using var conn = _factory.CreateConnection();

        var existing = await GetAsync(progress.User_Id, progress.Word_Id);

        if (existing == null)
        {
            progress.Id = Guid.NewGuid();
            progress.Count_Total_View = 1;
            progress.Count_Plus = success ? 1 : 0;
            progress.Count_Minus = success ? 0 : 1;
            progress.Progress = success ? 10 : 0;
            progress.Last_Review = DateTime.UtcNow;

            await conn.ExecuteAsync(@"
                INSERT INTO user_word_progress 
                (id, user_id, word_id, last_review, count_total_view, count_plus, count_minus, progress) 
                VALUES (@Id, @User_Id, @Word_Id, @Last_Review, @Count_Total_View, @Count_Plus, @Count_Minus, @Progress)", progress);
        }
        else
        {
            existing.Count_Total_View++;
            existing.Last_Review = DateTime.UtcNow;
            if (success) { existing.Count_Plus++; existing.Progress += 10; }
            else { existing.Count_Minus++; existing.Progress -= 5; }

            await conn.ExecuteAsync(@"
                UPDATE user_word_progress SET 
                    last_review = @Last_Review,
                    count_total_view = @Count_Total_View,
                    count_plus = @Count_Plus,
                    count_minus = @Count_Minus,
                    progress = @Progress
                WHERE user_id = @User_Id AND word_id = @Word_Id", existing);
        }

    }


}

```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\UserWordRepository.cs
```csharp
using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserWordRepository
{
    private readonly DbConnectionFactory _factory;

    public UserWordRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> UserHasWordAsync(Guid userId, string baseText)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM user_words uw
                JOIN words w ON uw.word_id = w.id
                WHERE uw.user_id = @User_Id AND LOWER(w.base_text) = LOWER(@Base_Text)
            )";
        //here is the bug with same words in different langs
        return await conn.ExecuteScalarAsync<bool>(sql, new { User_Id = userId, Base_Text = baseText });
    }

    public async Task AddUserWordAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        INSERT INTO user_words (user_id, word_id)
        VALUES (@User_Id, @Word_Id)
        ON CONFLICT DO NOTHING;
    ";
        await conn.ExecuteAsync(sql, new { User_Id = userId, Word_Id = wordId });
    }

    public async Task<bool> RemoveUserWordAsync(Guid userId, string word)
    {
        using var conn = _factory.CreateConnection();

        var sql = @"
        DELETE FROM user_words
        USING words
        WHERE user_words.word_id = words.id
          AND user_words.user_id = @User_Id
          AND LOWER(words.base_text) = LOWER(@Word);
    ";

        var affectedRows = await conn.ExecuteAsync(sql, new { User_Id = userId, Word = word });
        return affectedRows > 0;
    }


    public async Task<IEnumerable<Word>> GetWordsByUserId(Guid? userId)
    {
        if (userId == null) return new List<Word>();
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT 
            w.id AS Id,
            w.base_text AS Base_Text,
            w.language_id AS Language_Id
        FROM user_words uw
        JOIN words w ON uw.word_id = w.id
        WHERE uw.user_id = @User_Id
    ";
        return await conn.QueryAsync<Word>(sql, new { User_Id = userId });
    }

    public async Task<IEnumerable<Word>> GetWordsByUserId(Guid? userId, int? LangId)
    {
        if (userId == null || LangId == null) return new List<Word>();
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT 
            w.id AS Id,
            w.base_text AS Base_Text,
            w.language_id AS Language_Id
        FROM user_words uw
        JOIN words w ON uw.word_id = w.id
        WHERE uw.user_id = @User_Id AND w.language_id = @Lang_Id
    ";
        return await conn.QueryAsync<Word>(sql, new { User_Id = userId, Lang_Id = LangId });
    }

    public async Task<UserWord?> GetUserWordAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        SELECT user_id AS User_Id, word_id AS Word_Id, translation_id AS Translation_Id
        FROM user_words
        WHERE user_id = @User_Id AND word_id = @Word_Id
        LIMIT 1;
    ";
        return await conn.QueryFirstOrDefaultAsync<UserWord>(sql, new { User_Id = userId, Word_Id = wordId });

    }


    public async Task UpdateTranslationIdAsync(Guid userId, Guid wordId, Guid translationId)
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
        UPDATE user_words
        SET translation_id = @Translation_Id
        WHERE user_id = @User_Id AND word_id = @Word_Id;
        ";

        await conn.ExecuteAsync(sql, new
        {
            User_Id = userId,
            Word_Id = wordId,
            Translation_Id = translationId
        });
    }


    public async Task RemoveAllUserWords()
    {
        using var conn = _factory.CreateConnection();
        var sql = @"
            DELETE FROM user_words";
        await conn.ExecuteAsync(sql);
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Repositories\WordRepository.cs
```csharp
using Dapper;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using TelegramWordBot.Models;
using static System.Net.Mime.MediaTypeNames;

namespace TelegramWordBot.Repositories
{
    public class WordRepository
    {
        private readonly IConnectionFactory _factory;

        public WordRepository(IConnectionFactory factory)
        {
            _factory = factory;
        }

       

        public async Task<Word?> GetByTextAsync(string baseText)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Word>(
                "SELECT * FROM words WHERE LOWER(base_text) = LOWER(@Base_Text)",
                new { Base_Text = baseText });
        }

        public async Task<Word?> GetByTextAndLanguageAsync(string text, int languageId)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<Word>(
                "SELECT * FROM words WHERE LOWER(base_text) = LOWER(@Base_Text) AND language_id = @Language_Id",
                new { Base_Text = text, Language_Id = languageId });
        }


        public async Task<bool> WordExistsAsync(string baseText, int? languageId = null)
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT EXISTS (
                   SELECT 1 FROM words 
                   WHERE base_text = @Base_Text 
                   " + (languageId.HasValue ? "AND language_id = @Language_Id" : "") + ")";

            return await conn.ExecuteScalarAsync<bool>(sql, new { Base_Text = baseText, Language_Id = languageId });
        }

        public async Task<IEnumerable<Word>> GetAllWordsAsync()
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryAsync<Word>("SELECT * FROM words");
        }

        public async Task AddWordAsync(Word word)
        {
            //if (await WordExistsAsync(word.Base_Text, word.Language_Id)) return;
            var sql = @"INSERT INTO words (id, base_text, language_id)
                    VALUES (@Id, @Base_Text, @Language_Id)";

            using var conn = _factory.CreateConnection();
            await conn.ExecuteAsync(sql, word);
        }

        public async Task<Word?> GetWordById(Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            var sql = @"SELECT * FROM words WHERE id = @Word_Id";
           return await conn.QueryFirstOrDefaultAsync<Word>(sql, new {Word_Id = wordId} );
        }


        public async Task RemoveAllWords()
        {
            using var conn = _factory.CreateConnection();
            var sql = @"
            DELETE FROM words";
            await conn.ExecuteAsync(sql);
        }
    }

}

```

## Файл: ..\..\..\..\TelegramWordBot\Services\AIHelper.cs
```csharp
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services
{
    public interface IAIHelper
    {
        Task<TranslatedTextClass> TranslateWordAsync(string word, string sourceLangName, string targetLangName);
        Task<string> SimpleTranslateText(string text, string targetLang);
        Task<string> GetLangName(string text);
        Task<string> GetLangName(string text, IEnumerable<Language> languages);
    }

    class AIHelper: IAIHelper
    {
        private readonly HttpClient _http;
        private readonly string? _openAiKey;
        private readonly string _geminiKey;

        public AIHelper(HttpClient httpClient)
        {
            _http = httpClient;
            _openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");// ?? throw new InvalidOperationException("OPENAI_API_KEY is not set.");
            _geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? throw new InvalidOperationException("GEMINI_API_KEY is not set.");
        }
        public async Task<TranslatedTextClass> TranslateWordAsync(string srcText, string sourLangName, string targetLangName)
        {
            var oneWord = srcText.Split(' ').Count() == 1;
            string prompt = $"You are an expert translator specializing in {sourLangName} and {targetLangName} . " +
                $"Make all translations as accurately as possible.  ";
                
            if (oneWord)
                prompt += @"Respond ONLY in JSON format like this, with no explanations or conversational text: 
               {
                {
                    translations: [
                    { { text: 'translation_1', example: 'example_sentence_1' } },
                    { { text: 'translation_2_if_needed)', example: 'example_sentence_2' } },
                    { { error: 'error_message_if_it_is' } }
                                    ]
                }
            }" +
            $" Give a translation from {sourLangName} to {targetLangName} of this word - '{srcText}'. " ;
            else
                prompt += @"Respond ONLY in JSON format like this, with no explanations or conversational text: 
               {
                {
                    translations: [
                    { { text: 'translation' } },
                    { { error: 'error_message_if_it_is' } }
                                    ]
                }
            } "+
            $"Translate from {sourLangName} to {targetLangName} the text = '{srcText}' ";

            prompt += @"// --- Important: Ensure the JSON is valid and contains only the requested fields. Your answer is content of json.---";
            var response =  await TranslateWithGeminiAsync(prompt, false);
            TranslatedTextClass returnedTranslate = new TranslatedTextClass(response);
            return returnedTranslate;
           // return await TranslateWithOpenAIAsync(prompt);
        }

        public async Task<string> SimpleTranslateText(string text, string targetLang)
        {
            string prompt = $"Translate the text into language {targetLang}. " +
                $"The answer should contain only the translation text, without comments. " +
                $"The source text is: {text}";
            return await TranslateWithGeminiAsync(prompt, true);
        }

        
        public async Task<string>GetLangName(string text)
        {
            string prompt = $"Extract the language name from the following text: '{text}'." +
                $" Give your answer strictly in the format of one word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";
            return await TranslateWithGeminiAsync(prompt, true);
        }

        public async Task<string> GetLangName(string text, IEnumerable<Language> languages)
        {
            string prompt = "";
            if (languages == null || languages.Count() == 0) 
             prompt = $"Determine the language name of the following text: '{text}'." +
                $" Give your answer strictly in the format of one word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";
            else
            {
                var langsString = string.Join(", ", languages.Select(x => x.Name));
                prompt = $"Try to determine one language from ( {langsString} ) of the following text: '{text}'." +
                $" Give your answer strictly in the format of one word with a capital letter in english. " +
                $"If you can not do it - return only 'error'";

            }

            return await TranslateWithGeminiAsync(prompt, true);
        }

        private async Task<string> TranslateWithOpenAIAsync(string prompt)
        {
            var request = new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                new { role = "user", content = prompt }
            },
                temperature = 0.3
            };

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _openAiKey);
            httpRequest.Content = JsonContent.Create(request, options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var response = await _http.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var result = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return result?.Trim() ?? "";
        }

        private async Task<string> TranslateWithGeminiAsync(string prompt, bool lite)
        {
            var requestBody = new GeminiRequest
            {
                Contents = new List<Content>
            {
                new Content
                {
                    Parts = new List<Part> { new Part { Text = prompt } }
                }
            },
                GenerationConfig = new GenerationConfiguration
                {
                    Temperature = 0.0, // Для точности перевода
                    MaxOutputTokens = 450 // Ограничение для короткого текста
                },
                SafetySettings = new List<SafetySetting>
            {
                // ВАЖНО: Использование BLOCK_NONE отключает защиту.
                // Используйте с осторожностью и пониманием рисков.
                new SafetySetting { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_NONE" },
                new SafetySetting { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_NONE" }
            }
            };

            string url;
            if (lite)  url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent?key={_geminiKey}";
            else
                url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiKey}";

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody)
            };

            httpRequest.Headers.Add("Accept", "application/json");

            var response = await _http.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            return content?.Trim() ?? "";
        }


        public static string GetResponse(string input)
        {
            // Simulate AI response generation
            // In a real-world scenario, this would involve calling an AI model or API
            return $"AI Response to: {input}";
        }
    }
}

```

## Файл: ..\..\..\..\TelegramWordBot\Services\GeminiAPI.cs
```csharp
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace TelegramWordBot.Services
{


    public class GeminiRequest
    {
        [JsonPropertyName("contents")]
        public List<Content> Contents { get; set; }

        [JsonPropertyName("generationConfig")]
        public GenerationConfiguration GenerationConfig { get; set; }

        [JsonPropertyName("safetySettings")]
        public List<SafetySetting> SafetySettings { get; set; }
    }

    public class Content
    {
        [JsonPropertyName("parts")]
        public List<Part> Parts { get; set; }
    }

    public class Part
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class GenerationConfiguration
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; }

        // Можно добавить topP, topK, если понадобятся
        // [JsonPropertyName("topP")]
        // public double? TopP { get; set; } // Nullable, если не всегда используется

        // [JsonPropertyName("topK")]
        // public int? TopK { get; set; } // Nullable, если не всегда используется
    }

    public class SafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } // Например, "HARM_CATEGORY_HARASSMENT"

        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } // Например, "BLOCK_NONE"
    }


    // --- Классы для ОБРАБОТКИ ОТВЕТА ---
    // (Основано на типичной структуре ответа Gemini API)

    public class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate> Candidates { get; set; }

        // Могут быть и другие поля, например, promptFeedback
        // [JsonPropertyName("promptFeedback")]
        // public PromptFeedback PromptFeedback { get; set; }
    }

    public class Candidate
    {
        [JsonPropertyName("content")]
        public Content Content { get; set; } // Используем тот же класс Content

        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<SafetyRating> SafetyRatings { get; set; }
    }

    public class SafetyRating // Отличается от SafetySetting в запросе
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("probability")]
        public string Probability { get; set; } // Например, "NEGLIGIBLE"
    }

    
}
```

## Файл: ..\..\..\..\TelegramWordBot\Services\KeyboardFactory.cs
```csharp
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;

namespace TelegramWordBot.Services;

public static class KeyboardFactory
{
    // Главное меню
    public static ReplyKeyboardMarkup GetMainMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📚 Мои слова"), new KeyboardButton("➕ Добавить слово") },
            new[] { new KeyboardButton("📖 Учить"), new KeyboardButton("⚙️ Настройки") },
            new[] { new KeyboardButton("📊 Статистика"), new KeyboardButton("❓ Помощь") }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Инлайн-кнопки для конкретного слова (например, на карточке)
    public static InlineKeyboardMarkup GetWordCardInline(string word)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"delete:{word}"),
                InlineKeyboardButton.WithCallbackData("🔁 Повторить", $"repeat:{word}"),
                InlineKeyboardButton.WithCallbackData("⭐ В избранное", $"favorite:{word}")
            }
        });
    }

    // Настройки — выбор действия
    public static InlineKeyboardMarkup GetConfigInline()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🌐 Выбрать другой язык", "switch_language") },
            new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить новый язык", "add_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData("➖ Удалить текущий язык", "remove_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData("🌐 Изменить родной язык", "set_native") }
        });
    }

    // Отображение главного меню пользователю
    public static async Task ShowMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Главное меню:", replyMarkup: GetMainMenu(), cancellationToken: ct);
    }

    // Отображение меню настроек пользователю
    public static async Task ShowConfigMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Настройки:", replyMarkup: GetConfigInline(), cancellationToken: ct);
    }

    

    // Обработка команд с кнопок
    //public static async Task<(bool handled, string? newState)> HandleKeyboardCommandAsync(ITelegramBotClient botClient, ChatId chatId, string command,  CancellationToken ct)
    //{
    //    switch (command.ToLowerInvariant())
    //    {
    //        case "📚 мои слова":
    //            //await botClient.SendMessage(chatId, "Здесь будет список твоих слов.", cancellationToken: ct);
    //            return (true, null);

    //        case "➕ добавить слово":
    //            await botClient.SendMessage(chatId, "Введите слово для добавления:", cancellationToken: ct);
    //            return (true, "awaiting_addword");

    //        case "📖 учить":
    //            await botClient.SendMessage(chatId, "Режим обучения пока в разработке.", cancellationToken: ct);
    //            return (true, null);

    //        case "⚙️ настройки":
    //            await ShowConfigMenuAsync(botClient, chatId, ct);
    //            return (true, null);

    //        case "📊 статистика":
    //            await ShowStatisticsAsync(botClient, chatId, ct);
    //            return (true, null);

    //        case "❓ помощь":
    //            await botClient.SendMessage(chatId, "Я бот для изучения слов. Используй кнопки или команды: /addword, /learn, /config", cancellationToken: ct);
    //            return (true, null);

    //        default:
    //            return (false, null);
    //    }
    //}
}

```

## Файл: ..\..\..\..\TelegramWordBot\Services\TelegramMessageHelper.cs
```csharp
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramWordBot.Services;

public class TelegramMessageHelper
{
    private readonly ITelegramBotClient _bot;
    public TelegramMessageHelper(ITelegramBotClient botClient)
    {
        _bot = botClient;
    }

    // === Вспомогательный метод для генерации текста карточки слова ===
    public string GenerateWordCardText(string word, string translation, string? example = null, string? category = null)
    {
        var text = $"<b>{EscapeHtml(word)}</b>\n<i>{EscapeHtml(translation)}</i>";

        if (!string.IsNullOrWhiteSpace(example))
            text += $"\n\n📘 Пример: {EscapeHtml(example)}";

        if (!string.IsNullOrWhiteSpace(category))
            text += $"\n🔖 Категория: {EscapeHtml(category)}";

        return text;
    }

    

    // === Базовые методы отправки сообщений ===
    public async Task<Message> SendWordCard(ChatId chatId, string word, string translation, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var text = GenerateWordCardText(word, translation, example, category);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return await _bot.SendPhoto(
                chatId: chatId,
                photo: new InputFileUrl(imageUrl),
                caption: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        else
        {
            return await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    public async Task<Message> SendWordCardWithActions(ChatId chatId, string word, string translation, int wordId, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"edit_{wordId}"),
            InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"delete_{wordId}")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("🔁 Повторить", $"repeat_{wordId}"),
            InlineKeyboardButton.WithCallbackData("✅ Выучено", $"learned_{wordId}")
        }
    });

        var text = GenerateWordCardText(word, translation, example, category);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return await _bot.SendPhoto(
                chatId: chatId,
                photo: new InputFileUrl(imageUrl),
                caption: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
        else
        {
            return await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
    }

    public async Task<Message> EditWordCard(ChatId chatId, int messageId, string word, string translation, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var text = GenerateWordCardText(word, translation, example, category);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            var media = new InputMediaPhoto(new InputFileUrl(imageUrl))
            {
                Caption = text,
                ParseMode = ParseMode.Html
            };

            return await _bot.EditMessageMedia(
                chatId: chatId,
                messageId: messageId,
                media: media,
                cancellationToken: ct);
        }
        else
        {
            return await _bot.EditMessageText(
                chatId: chatId,
                messageId: messageId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    public async Task<Message> ShowWordSlider(ChatId chatId, int langId, 
        int currentIndex, int totalWords, string word, string translation, 
        string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var buttons = new List<InlineKeyboardButton>();

        if (currentIndex > 0)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData(
                text: "⬅️ Назад",
                callbackData: $"prev:{langId}:{currentIndex - 1}"
            ));
        }

        if (currentIndex < totalWords - 1)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData(
                text: "➡️ Вперед",
                callbackData: $"next:{langId}:{currentIndex + 1}"
            ));
        }

        var keyboard = new InlineKeyboardMarkup(new[] { buttons.ToArray() });

        // Генерируем текст карточки (можно вынести в общий метод)
        var text = GenerateWordCardText(word, translation, example, category)
                 + $"\n\n📄 {currentIndex + 1}/{totalWords}";

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            return await _bot.SendPhoto(
                chatId: chatId,
                photo: new InputFileUrl(imageUrl),
                caption: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
        else
        {
            return await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }
    }

    public async Task<Message> SendConfirmationDialog(ChatId chatId, string question, string confirmCallback, string cancelCallback, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Да", confirmCallback),
            InlineKeyboardButton.WithCallbackData("❌ Нет", cancelCallback)
        }
    });

        return await _bot.SendMessage(
            chatId: chatId,
            text: $"❓ {EscapeHtml(question)}",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task<Message> EditMessageWithNewButtons(ChatId chatId, int messageId, string newText, InlineKeyboardMarkup newButtons, CancellationToken ct = default)
    {
        return await _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: newText,
            parseMode: ParseMode.Html,
            replyMarkup: newButtons,
            cancellationToken: ct);
    }

    public async Task SendWordCardAsync(ChatId chatId, string word, string translation, string? imageUrl, CancellationToken ct)
    {
        var text = $"<b>{EscapeHtml(word)}</b>\n<i>{EscapeHtml(translation)}</i>";

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await _bot.SendPhoto(
                chatId: chatId,
                photo: InputFile.FromUri(imageUrl),
                caption: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
        else
        {
            await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    /// <summary>
    /// Отправляет произвольное HTML-сообщение.
    /// </summary>
    public async Task<Message> SendText(
        ChatId chatId,
        string text,
        CancellationToken ct = default)
    {
        return await _bot.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    public async Task SendErrorAsync(ChatId chatId, string message, CancellationToken ct)
    {
        var text = $"❌ <i>{EscapeHtml(message)}</i>";
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    public async Task SendSuccessAsync(ChatId chatId, string message, CancellationToken ct)
    {
        var text = $"✅ <b>{EscapeHtml(message)}</b>";
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }
    public async Task SendInfoAsync(ChatId chatId, string message, CancellationToken ct)
    {
        var text = $"ℹ️<i>{EscapeHtml(message)}</i>";
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    public static string EscapeHtml(string input) =>
        input.Replace("&", "&amp;");//.Replace("<", "&lt;").Replace(">", "&gt;");

    //private string EscapeHtml(string input) =>
    //    input.Replace("&", "&amp;")
    //         .Replace("<", "&lt;")
    //         .Replace(">", "&gt;")
    //         .Replace("\"", "&quot;")
    //         .Replace("'", "&#39;");
}

```

## Файл: ..\..\..\..\TelegramWordBot\Services\TranslatedTextClass.cs
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json; 
using System.Threading.Tasks; 

namespace TelegramWordBot.Services 
{
    public class TranslatedTextClass
    {
        string? translatedText; 
        string? error;
        List<string>? examples;

        public string? TranslatedText { get => translatedText; set => translatedText = value; }
        public List<string>? Examples { get => examples; set => examples = value; }
        public string? Error { get => error; set => error = value; }

        public TranslatedTextClass(string json)
        {
            examples = new List<string>();
            json = json.Trim().Trim('`');
            var startIndex = json.IndexOf('{');
            var endIndex = json.LastIndexOf('}');
            json = json.Substring(startIndex, endIndex - startIndex + 1);

            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;

                    if (root.TryGetProperty("error", out JsonElement errorElement) && errorElement.ValueKind == JsonValueKind.String)
                    {
                        this.error = errorElement.GetString();
                        // Если есть ошибка верхнего уровня, остальное не парсим
                        return; 
                    }

                    //Парсим массив translations
                    if (root.TryGetProperty("translations", out JsonElement translationsElement) && translationsElement.ValueKind == JsonValueKind.Array)
                    {
                        bool firstTextFound = false; // Флаг, чтобы взять только первый 'text'
                        foreach (JsonElement translationItem in translationsElement.EnumerateArray())
                        {
                            if (translationItem.ValueKind == JsonValueKind.Object)
                            {
                                // Пытаемся извлечь 'text'
                                if ( translationItem.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
                                {
                                    if (!firstTextFound)
                                    {
                                        this.translatedText = textElement.GetString();
                                        firstTextFound = true; // Устанавливаем флаг, что основной текст найден
                                    }
                                    else
                                    {
                                        this.translatedText +=", " + textElement.GetString();
                                    }
                                }

                                // Пытаемся извлечь 'example' (независимо от 'text')
                                if (translationItem.TryGetProperty("example", out JsonElement exampleElement) && exampleElement.ValueKind == JsonValueKind.String)
                                {
                                    string? exampleValue = exampleElement.GetString();
                                    if (!string.IsNullOrEmpty(exampleValue)) // Добавляем только непустые примеры
                                    {
                                        this.examples ??= new List<string>();
                                        this.examples.Add(exampleValue);
                                    }
                                }

                                // Обработка ошибки *внутри* элемента массива
                                // Если основной текст еще не найден и есть ошибка в элементе
                                if (!firstTextFound && string.IsNullOrEmpty(this.error) && translationItem.TryGetProperty("error", out JsonElement itemErrorElement) && itemErrorElement.ValueKind == JsonValueKind.String)
                                {
                                    this.error = itemErrorElement.GetString();
                                    // Можно решить: прервать парсинг массива или продолжить собирать примеры?
                                    // В данном варианте продолжаем, чтобы собрать все возможные данные
                                }
                            }
                        }
                        // Если после цикла не нашли ни одного 'text', а ошибки не было
                        if (!firstTextFound && string.IsNullOrEmpty(this.error))
                        {
                         this.error = "Invalid response format: No 'text' found in translations array.";
                        }

                        // Если после парсинга массив примеров пуст, устанавливаем его в null для консистентности
                        if (this.examples != null && this.examples.Count == 0)
                        {
                            this.examples = null;
                        }
                    }
                    else
                    {
                        // Если нет поля 'translations' (и нет ошибки), считаем это проблемой формата
                        // Либо это может быть случай одного перевода без массива (нужно уточнять формат)
                        // Если формат ТОЛЬКО с массивом, то это ошибка:
                        if (string.IsNullOrEmpty(this.error)) // Устанавливаем ошибку, только если ее еще нет
                        {
                            this.error = "Invalid response format: Missing or invalid 'translations' array.";
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                // Если JSON некорректный, записываем ошибку
                Console.WriteLine($"JSON Parsing Error: {ex.Message}"); // Логирование для отладки
                this.error = $"Failed to parse JSON response: {ex.Message}";
                // Обнуляем остальные поля на всякий случай
                this.translatedText = null;
                this.examples = null;
            }
            catch (Exception ex) // Ловим другие возможные ошибки
            {
                Console.WriteLine($"Unexpected Error during translation object creation: {ex.Message}"); // Логирование
                this.error = $"An unexpected error occurred while processing the translation: {ex.Message}";
                this.translatedText = null;
                this.examples = null;
            }
        }

        public string GetExampleString()
        {
            StringBuilder sb = new StringBuilder();
            if (Examples != null && Examples.Count != 0)
            {
                foreach (var ex in Examples)
                    sb.AppendLine(ex);                
            }
            return sb.ToString();
        }
        public bool IsSuccess()
        {
            return string.IsNullOrEmpty(this.error) && !string.IsNullOrEmpty(this.translatedText);
        }
    }
}
```
