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

    // Отображение статистики (заглушка)
    public static async Task ShowStatisticsAsync(ITelegramBotClient botClient, ChatId chatId, CancellationToken ct)
    {
        await botClient.SendMessage(chatId, "📊 Статистика: (в разработке)", cancellationToken: ct);
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
