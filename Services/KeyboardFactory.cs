using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramWordBot.Services;

public static class KeyboardFactory
{
    // Главное меню
    public static ReplyKeyboardMarkup GetMainMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📚 Мои слова"), new KeyboardButton("➕ Добавить слово") },
            new[] { new KeyboardButton("📊 Статистика"), new KeyboardButton("📖 Учить") },
            new[] { new KeyboardButton("🌐 Настройки"), new KeyboardButton("👤 Профиль") }
        })
        {
            ResizeKeyboard = true
        };
    }

    // Подменю для раздела "Мои слова"
    public static ReplyKeyboardMarkup GetMyWordsMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🔍 Показать все слова") },
            new[] { new KeyboardButton("📁 Словари по темам") },
            new[] { new KeyboardButton("Генерация новых слов") },
            new[] { new KeyboardButton("📝 Изменить слово") },
            new[] { new KeyboardButton("🗑️ Удалить слова") },
            new[] { new KeyboardButton("♻️ Обнулить прогресс слов") },
            new[] { new KeyboardButton("⬅️ Назад") }
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
            new[] { InlineKeyboardButton.WithCallbackData("🌐 Выбрать язык изучения", "switch_language") },
            new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить язык", "add_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData("🔿️ Удалить язык", "remove_foreign") },
            new[] { InlineKeyboardButton.WithCallbackData("🌐 Родной язык", "set_native") },
            new[] { InlineKeyboardButton.WithCallbackData("🎓 Режим обучения", "config_learn:main") },
            new[] { InlineKeyboardButton.WithCallbackData("❓ Помощь", "help_info") }
        });
    }

    public static InlineKeyboardMarkup GetConfigLearnInline(Models.User user)
    {
        if (user.Prefer_Multiple_Choice)
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Вспомнил/Не вспомнил", "config_learn:binary") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Выбор из вариантов", "config_learn:multiple") }
        });
        else
            return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Вспомнил/Не вспомнил", "config_learn:binary") },
            new[] { InlineKeyboardButton.WithCallbackData("Выбор из вариантов", "config_learn:multiple") }
        });
    }

    // Инлайн-клавиатура для меню статистики
    public static InlineKeyboardMarkup GetStatisticsInline()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("📅 За сегодня", "stat_today") },
            new[] { InlineKeyboardButton.WithCallbackData("📈 Общий прогресс", "stat_total") },
            new[] { InlineKeyboardButton.WithCallbackData("🔍 По языкам", "stat_languages") }
        });
    }

    // Инлайн-клавиатура для профиля
    public static InlineKeyboardMarkup GetProfileInline(Guid userId, long telegramId, string appUrl)
    {
        var baseUrl = string.IsNullOrEmpty(appUrl) ? string.Empty : appUrl.TrimEnd('/');
        var todoUrl = $"{baseUrl}/todoitems/pretty?userId={userId}";

        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("👤 Инфо о пользователе", "profile_info") }
        };

        var adminId = Environment.GetEnvironmentVariable("ADMIN_ID");
        if (!string.IsNullOrEmpty(adminId) && adminId == telegramId.ToString())
        {
            rows.Add(new[] { InlineKeyboardButton.WithWebApp("📝 Todo App", new WebAppInfo(todoUrl)) });
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Сбросить статистику", "reset_profile_stats") });

        return new InlineKeyboardMarkup(rows);
    }

    // Инлайн-кнопки для управления словарями
    public static InlineKeyboardMarkup GetDictionaryManageInline(int id)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"edit_dict:{id}"),
                InlineKeyboardButton.WithCallbackData("🔄 Обнулить прогресс", $"reset_dict:{id}"),
                InlineKeyboardButton.WithCallbackData("🗑️ Удалить словарь", $"delete_dict:{id}")
            }
        });
    }

    public static InlineKeyboardMarkup GetDictionaryListInline(IEnumerable<Models.Dictionary> dictionaries)
    {
        var rows = new List<InlineKeyboardButton[]>();
        foreach (var d in dictionaries)
        {
            var name = d.Name == "default" ? "Общий" : d.Name;
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(name, $"show_dict:{d.Id}")
            });
        }
        rows.Add(new[]{ InlineKeyboardButton.WithCallbackData("Создать новый", $"create_dict:new") });
        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup GetTopicDictionaryActions(Guid dictId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🗑️ Удалить словарь (без слов)", $"delete_dict:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🗑️ Удалить словарь и слова", $"delete_dict_full:{dictId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🗑️ Удалить несколько слов", $"delete_words:{dictId}") }
        });
    }

    public static ReplyKeyboardMarkup GetTopicDictionaryMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("🗑️ Удалить словарь") },
            new[] { new KeyboardButton("🗑️ Удалить несколько слов") },
            new[] { new KeyboardButton("⬅️ Назад") }
        })
        {
            ResizeKeyboard = true
        };
    }

    public static async Task ShowTopicDictionaryMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Словарь:", replyMarkup: GetTopicDictionaryMenu(), cancellationToken: ct);
    }

    // Отображение главного меню пользователю
    public static async Task ShowMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Главное меню:", replyMarkup: GetMainMenu(), cancellationToken: ct);
    }

    public static async Task HideMainMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Меню скрыто.", replyMarkup: new ReplyKeyboardRemove(), cancellationToken: ct);
    }

    // Отображение меню настроек пользователю
    public static async Task ShowConfigMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Настройки:", replyMarkup: GetConfigInline(), cancellationToken: ct);
    }

    public static async Task ShowMyWordsMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Мои слова:", replyMarkup: GetMyWordsMenu(), cancellationToken: ct);
    }

    public static async Task ShowLearnConfig(ITelegramBotClient botClient, ChatId chatId, Models.User user, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Режим показа слов при обучении", replyMarkup: GetConfigLearnInline(user), cancellationToken: ct);
    }

    // Отображение меню статистики
    public static async Task ShowStatisticsMenuAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Статистика:", replyMarkup: GetStatisticsInline(), cancellationToken: ct);
    }

    // Отображение меню профиля
    public static async Task ShowProfileMenuAsync(ITelegramBotClient botClient, ChatId chatId, Guid userId, long telegramId, string appUrl, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "Профиль:", replyMarkup: GetProfileInline(userId, telegramId, appUrl), cancellationToken: ct);
    }
        
}
