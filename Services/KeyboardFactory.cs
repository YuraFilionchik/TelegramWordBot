using Microsoft.Extensions.Localization;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramWordBot.Services;

//TODO: Рассмотреть возможность сделать этот класс нестатическим и внедрять IStringLocalizer<KeyboardFactory> через конструктор,
// если предполагается его использование в тестах или более сложная логика.
// Пока что для простоты оставим статическим и будем передавать IStringLocalizer в каждый метод.
public static class KeyboardFactory
{
    // Главное меню
    public static ReplyKeyboardMarkup GetMainMenu(IStringLocalizer<KeyboardFactory> localizer)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(localizer["Keyboard.MainMenu.MyWords"]), new KeyboardButton(localizer["Keyboard.MainMenu.AddWord"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MainMenu.Statistics"]), new KeyboardButton(localizer["Keyboard.MainMenu.Learn"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MainMenu.Settings"]), new KeyboardButton(localizer["Keyboard.MainMenu.Profile"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Подменю для раздела "Мои слова"
    public static ReplyKeyboardMarkup GetMyWordsMenu(IStringLocalizer<KeyboardFactory> localizer)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.ShowAllWords"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.DictionariesByTopics"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.GenerateNewWords"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.EditWord"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.DeleteWords"]) },
            new[] { new KeyboardButton(localizer["Keyboard.MyWordsMenu.Back"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Инлайн-кнопки для конкретного слова (например, на карточке)
    public static InlineKeyboardMarkup GetWordCardInline(IStringLocalizer<KeyboardFactory> localizer, string word)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.WordCard.Delete"], $"delete:{word}"),
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.WordCard.Repeat"], $"repeat:{word}"),
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.WordCard.Favorite"], $"favorite:{word}")
            }
        });
    }

    // Настройки — выбор действия
    public static InlineKeyboardMarkup GetConfigInline(IStringLocalizer<KeyboardFactory> localizer)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.SwitchLanguage"], "switch_language") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.AddLanguage"], "add_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.RemoveLanguage"], "remove_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.NativeLanguage"], "set_native") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.LearningMode"], "config_learn:main") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Config.Help"], "help_info") }
        });
    }

    public static InlineKeyboardMarkup GetConfigLearnInline(IStringLocalizer<KeyboardFactory> localizer, Models.User user)
    {
        if (user.Prefer_Multiple_Choice)
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.LearnConfig.BinaryChoice"], "config_learn:binary") },
                new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.LearnConfig.MultipleChoiceSelected"], "config_learn:multiple") }
            });
        else
            return new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.LearnConfig.BinaryChoiceSelected"], "config_learn:binary") },
                new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.LearnConfig.MultipleChoice"], "config_learn:multiple") }
            });
    }

    // Инлайн-клавиатура для меню статистики
    public static InlineKeyboardMarkup GetStatisticsInline(IStringLocalizer<KeyboardFactory> localizer)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Statistics.Today"], "stat_today") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Statistics.TotalProgress"], "stat_total") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Statistics.ByLanguage"], "stat_languages") }
        });
    }

    // Инлайн-клавиатура для профиля
    public static InlineKeyboardMarkup GetProfileInline(IStringLocalizer<KeyboardFactory> localizer, Guid userId, long telegramId, string appUrl)
    {
        var baseUrl = string.IsNullOrEmpty(appUrl) ? string.Empty : appUrl.TrimEnd('/');
        var todoUrl = $"{baseUrl}/todoitems/pretty?userId={userId}";
        var adminUrl = $"{baseUrl}/admin?telegramId={telegramId}";

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Profile.UserInfo"], "profile_info") }
        };

        var adminId = Environment.GetEnvironmentVariable("ADMIN_ID");
        if (!string.IsNullOrEmpty(adminId) && adminId == telegramId.ToString())
        {
            rows.Add(new[] { InlineKeyboardButton.WithWebApp(localizer["Keyboard.Profile.TodoApp"], new WebAppInfo(todoUrl)) });
            rows.Add(new[] { InlineKeyboardButton.WithWebApp(localizer["Keyboard.Profile.AdminDashboard"], new WebAppInfo(adminUrl)) });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.Profile.ResetStatistics"], "reset_profile_stats") });

        return new InlineKeyboardMarkup(rows);
    }

    // Инлайн-кнопки для управления словарями
    public static InlineKeyboardMarkup GetDictionaryManageInline(IStringLocalizer<KeyboardFactory> localizer, int id)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.DictionaryManage.Edit"], $"edit_dict:{id}"),
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.DictionaryManage.ResetProgress"], $"reset_dict:{id}"),
                InlineKeyboardButton.WithCallbackData(localizer["Keyboard.DictionaryManage.Delete"], $"delete_dict:{id}")
            }
        });
    }

    public static InlineKeyboardMarkup GetDictionaryListInline(IStringLocalizer<KeyboardFactory> localizer, IEnumerable<Models.Dictionary> dictionaries)
    {
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var d in dictionaries)
        {
            var name = d.Name == "default" ? localizer["Keyboard.DictionaryList.DefaultName"] : d.Name;
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(name, $"show_dict:{d.Id}")
            });
        }
        rows.Add(new[]{ InlineKeyboardButton.WithCallbackData(localizer["Keyboard.DictionaryList.CreateNew"], $"create_dict:new") });
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup GetTopicDictionaryActions(IStringLocalizer<KeyboardFactory> localizer, Guid dictId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.TopicDictionary.DeleteDictionaryNoWords"], $"delete_dict:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.TopicDictionary.DeleteDictionaryAndWords"], $"delete_dict_full:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData(localizer["Keyboard.TopicDictionary.DeleteMultipleWords"], $"delete_words:{dictId}") }
        });
    }

    public static ReplyKeyboardMarkup GetTopicDictionaryMenu(IStringLocalizer<KeyboardFactory> localizer)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(localizer["Keyboard.TopicDictionaryMenu.DeleteDictionary"]) },
            new[] { new KeyboardButton(localizer["Keyboard.TopicDictionaryMenu.DeleteMultipleWords"]) },
            new[] { new KeyboardButton(localizer["Keyboard.TopicDictionaryMenu.Back"]) }
        })
        {
            ResizeKeyboard = true
        };
    }

    public static async Task ShowTopicDictionaryMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowTopicDictionaryMenu.Dictionary"], replyMarkup: GetTopicDictionaryMenu(localizer), cancellationToken: ct);
    }

    // Отображение главного меню пользователю
    public static async Task ShowMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowMainMenu.MainMenu"], replyMarkup: GetMainMenu(localizer), cancellationToken: ct);
    }

    public static async Task HideMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.HideMainMenu.MenuHidden"], replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
    }

    // Отображение меню настроек пользователю
    public static async Task ShowConfigMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowConfigMenu.Settings"], replyMarkup: GetConfigInline(localizer), cancellationToken: ct);
    }

    public static async Task ShowMyWordsMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowMyWordsMenu.MyWords"], replyMarkup: GetMyWordsMenu(localizer), cancellationToken: ct);
    }

    public static async Task ShowLearnConfig(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, Models.User user, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowLearnConfig.LearnModePrompt"], replyMarkup: GetConfigLearnInline(localizer, user), cancellationToken: ct);
    }

    // Отображение меню статистики
    public static async Task ShowStatisticsMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowStatisticsMenu.Statistics"], replyMarkup: GetStatisticsInline(localizer), cancellationToken: ct);
    }

    // Отображение меню профиля
    public static async Task ShowProfileMenuAsync(ITelegramBotClient botClient, ChatId chatId, IStringLocalizer<KeyboardFactory> localizer, Guid userId, long telegramId, string appUrl, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, localizer["Keyboard.ShowProfileMenu.Profile"], replyMarkup: GetProfileInline(localizer, userId, telegramId, appUrl), cancellationToken: ct);
    }
}
