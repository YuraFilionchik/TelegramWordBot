using Telegram.Bot; 
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types; 
using Telegram.Bot.Types.InputFiles;

namespace TelegramWordBot;

public class TelegramMessageHelper {
    private readonly ITelegramBotClient _bot;

    public TelegramMessageHelper(ITelegramBotClient botClient) {
        _bot = botClient;
    }

    public async Task SendWordCardAsync(ChatId chatId, string word, string translation, string? imageUrl, CancellationToken ct) {
        var text = $"<b>{EscapeHtml(word)}</b>\n<i>{EscapeHtml(translation)}</i>";

        if (!string.IsNullOrWhiteSpace(imageUrl)) {
            await _bot.SendPhotoAsync(
                chatId: chatId,
                photo: new InputOnlineFile(imageUrl),
                caption: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        } else
        {
            await _bot.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }
    }

    public async Task SendErrorAsync(ChatId chatId, string message, CancellationToken ct) {
        var text = $"❌ <i>{EscapeHtml(message)}</i>";
        await _bot.SendTextMessageAsync(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    public async Task SendSuccessAsync(ChatId chatId, string message, CancellationToken ct) {
        var text = $"✅ <b>{EscapeHtml(message)}</b>";
        await _bot.SendTextMessageAsync(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    private string EscapeHtml(string input) =>
    input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

}