using Microsoft.Extensions.Localization;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramWordBot.Services;

//TODO: Рассмотреть возможность сделать этот класс нестатическим и внедрять IStringLocalizer<KeyboardFactory> через конструктор,
// если предполагается его использование в тестах или более сложная логика.
// если предполагается его использование в тестах или более сложная логика.
public class KeyboardFactory
{
    private readonly IStringLocalizer<KeyboardFactory> _localizer;

    public KeyboardFactory(IStringLocalizer<KeyboardFactory> localizer)
    {
        _localizer = localizer;
    }

    // Главное меню
    public ReplyKeyboardMarkup GetMainMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(_localizer["Keyboard.MainMenu.MyWords"]), new KeyboardButton(_localizer["Keyboard.MainMenu.AddWord"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MainMenu.Statistics"]), new KeyboardButton(_localizer["Keyboard.MainMenu.Learn"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MainMenu.Settings"]), new KeyboardButton(_localizer["Keyboard.MainMenu.Profile"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Подменю для раздела "Мои слова"
    public ReplyKeyboardMarkup GetMyWordsMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.ShowAllWords"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.DictionariesByTopics"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.GenerateNewWords"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.EditWord"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.DeleteWords"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.MyWordsMenu.Back"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Инлайн-кнопки для конкретного слова (например, на карточке)
    public InlineKeyboardMarkup GetWordCardInline(string word)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.WordCard.Delete"], $"delete:{word}"),
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.WordCard.Repeat"], $"repeat:{word}"),
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.WordCard.Favorite"], $"favorite:{word}")
            }
        });
    }

    // Настройки — выбор действия
    public InlineKeyboardMarkup GetConfigInline()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.SwitchLanguage"], "switch_language") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.AddLanguage"], "add_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.RemoveLanguage"], "remove_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.NativeLanguage"], "set_native") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.LearningMode"], "config_learn:main") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Config.Help"], "help_info") }
        });
    }

    public InlineKeyboardMarkup GetConfigLearnInline(Models.User user)
    {
        if (user.Prefer_Multiple_Choice)
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.LearnConfig.BinaryChoice"], "config_learn:binary") },
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.LearnConfig.MultipleChoiceSelected"], "config_learn:multiple") }
            });
        else
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.LearnConfig.BinaryChoiceSelected"], "config_learn:binary") },
                new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.LearnConfig.MultipleChoice"], "config_learn:multiple") }
            });
    }

    // Инлайн-клавиатура для меню статистики
    public InlineKeyboardMarkup GetStatisticsInline()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Statistics.Today"], "stat_today") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Statistics.TotalProgress"], "stat_total") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Statistics.ByLanguage"], "stat_languages") }
        });
    }

    // Инлайн-клавиатура для профиля
    public InlineKeyboardMarkup GetProfileInline(Guid userId, long telegramId, string appUrl)
    {
        var baseUrl = string.IsNullOrEmpty(appUrl) ? string.Empty : appUrl.TrimEnd('/');
        var todoUrl = $"{baseUrl}/todoitems/pretty?userId={userId}";
        var adminUrl = $"{baseUrl}/admin?telegramId={telegramId}";

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Profile.UserInfo"], "profile_info") }
        };

        var adminId = Environment.GetEnvironmentVariable("ADMIN_ID");
        if (!string.IsNullOrEmpty(adminId) && adminId == telegramId.ToString())
        {
            rows.Add(new[] { InlineKeyboardButton.WithWebApp(_localizer["Keyboard.Profile.TodoApp"], new WebAppInfo(todoUrl)) });
            rows.Add(new[] { InlineKeyboardButton.WithWebApp(_localizer["Keyboard.Profile.AdminDashboard"], new WebAppInfo(adminUrl)) });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.Profile.ResetStatistics"], "reset_profile_stats") });

        return new InlineKeyboardMarkup(rows);
    }

    // Инлайн-кнопки для управления словарями
    public InlineKeyboardMarkup GetDictionaryManageInline(int id)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.DictionaryManage.Edit"], $"edit_dict:{id}"),
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.DictionaryManage.ResetProgress"], $"reset_dict:{id}"),
                InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.DictionaryManage.Delete"], $"delete_dict:{id}")
            }
        });
    }

    public InlineKeyboardMarkup GetDictionaryListInline(IEnumerable<Models.Dictionary> dictionaries)
    {
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var d in dictionaries)
        {
            var name = d.Name == "default" ? _localizer["Keyboard.DictionaryList.DefaultName"] : d.Name;
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(name, $"show_dict:{d.Id}")
            });
        }
        rows.Add(new[]{ InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.DictionaryList.CreateNew"], $"create_dict:new") });
        return new InlineKeyboardMarkup(rows);
    }

    public InlineKeyboardMarkup GetTopicDictionaryActions(Guid dictId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.TopicDictionary.DeleteDictionaryNoWords"], $"delete_dict:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.TopicDictionary.DeleteDictionaryAndWords"], $"delete_dict_full:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData(_localizer["Keyboard.TopicDictionary.DeleteMultipleWords"], $"delete_words:{dictId}") }
        });
    }

    public ReplyKeyboardMarkup GetTopicDictionaryMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(_localizer["Keyboard.TopicDictionaryMenu.DeleteDictionary"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.TopicDictionaryMenu.DeleteMultipleWords"]) },
            new[] { new KeyboardButton(_localizer["Keyboard.TopicDictionaryMenu.Back"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    public async Task ShowTopicDictionaryMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowTopicDictionaryMenu.Dictionary"], replyMarkup: GetTopicDictionaryMenu(), cancellationToken: ct);
    }

    // Отображение главного меню пользователю
    public async Task ShowMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowMainMenu.MainMenu"], replyMarkup: GetMainMenu(), cancellationToken: ct);
    }

    public async Task HideMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.HideMainMenu.MenuHidden"], replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
    }

    // Отображение меню настроек пользователю
    public async Task ShowConfigMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowConfigMenu.Settings"], replyMarkup: GetConfigInline(), cancellationToken: ct);
    }

    public async Task ShowMyWordsMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowMyWordsMenu.MyWords"], replyMarkup: GetMyWordsMenu(), cancellationToken: ct);
    }

    public async Task ShowLearnConfig(ITelegramBotClient botClient, ChatId chatId, Models.User user, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowLearnConfig.LearnModePrompt"], replyMarkup: GetConfigLearnInline(user), cancellationToken: ct);
    }

    // Отображение меню статистики
    public async Task ShowStatisticsMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowStatisticsMenu.Statistics"], replyMarkup: GetStatisticsInline(), cancellationToken: ct);
    }

    // Отображение меню профиля
    public async Task ShowProfileMenuAsync(ITelegramBotClient botClient, ChatId chatId, Guid userId, long telegramId, string appUrl, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, _localizer["Keyboard.ShowProfileMenu.Profile"], replyMarkup: GetProfileInline(userId, telegramId, appUrl), cancellationToken: ct);
    }
}
