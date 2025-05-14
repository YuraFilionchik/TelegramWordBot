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
    private string GenerateWordCardText(string word, string translation, string? example = null, string? category = null)
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

    public async Task<Message> ShowWordSlider(ChatId chatId, int currentWordIndex, int totalWords, string word, string translation, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var keyboardButtons = new List<InlineKeyboardButton>();

        if (currentWordIndex > 0)
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ Назад", $"prev_{currentWordIndex - 1}"));

        if (currentWordIndex < totalWords - 1)
            keyboardButtons.Add(InlineKeyboardButton.WithCallbackData("➡️ Вперед", $"next_{currentWordIndex + 1}"));

        var keyboard = new InlineKeyboardMarkup(keyboardButtons);

        var text = GenerateWordCardText(word, translation, example, category);
        text += $"\n\n📄 {currentWordIndex + 1}/{totalWords}";

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

    private string EscapeHtml(string input) =>
        input.Replace("&", "&amp;");//.Replace("<", "&lt;").Replace(">", "&gt;");

    //private string EscapeHtml(string input) =>
    //    input.Replace("&", "&amp;")
    //         .Replace("<", "&lt;")
    //         .Replace(">", "&gt;")
    //         .Replace("\"", "&quot;")
    //         .Replace("'", "&#39;");
}
