using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System;
using System.IO;
using TelegramWordBot.Services.TTS;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services;

public class TelegramMessageHelper
{
    private readonly ITelegramBotClient _bot;
    private readonly ITextToSpeechService _tts;
    private readonly TtsOptions _ttsOptions;

    public TelegramMessageHelper(ITelegramBotClient botClient, ITextToSpeechService tts, TtsOptions options)
    {
        _bot = botClient;
        _tts = tts;
        _ttsOptions = options;
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

        Message msg;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (IsHttpUrl(imageUrl))
            {
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileUrl(imageUrl),
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else if (File.Exists(imageUrl))
            {
                await using var stream = File.OpenRead(imageUrl);
                var file = InputFile.FromStream(stream, Path.GetFileName(imageUrl));
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: file,
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else
            {
                msg = await _bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
        }
        else
        {
            msg = await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }

        var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, audioText, ct);
        return msg;
    }

    public async Task<Message> SendWordCardWithEdit(ChatId chatId, string word, string translation, Guid wordId, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("✏️ Изменить", $"edit:{wordId}"));

        var text = GenerateWordCardText(word, translation, example, category);

        Message msg;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (IsHttpUrl(imageUrl))
            {
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileUrl(imageUrl),
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else if (File.Exists(imageUrl))
            {
                await using var stream = File.OpenRead(imageUrl);
                var file = InputFile.FromStream(stream, Path.GetFileName(imageUrl));
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: file,
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else
            {
                msg = await _bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
        }
        else
        {
            msg = await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }

        var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, audioText, ct);
        return msg;
    }

    public async Task<Message> EditWordCard(ChatId chatId, int messageId, string word, string translation, string? example = null, string? category = null, string? imageUrl = null, CancellationToken ct = default)
    {
        var text = GenerateWordCardText(word, translation, example, category);

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            InputMediaPhoto media;
            if (IsHttpUrl(imageUrl))
            {
                media = new InputMediaPhoto(new InputFileUrl(imageUrl))
                {
                    Caption = text,
                    ParseMode = ParseMode.Html
                };
            }
            else if (File.Exists(imageUrl))
            {
                await using var stream = File.OpenRead(imageUrl);
                media = new InputMediaPhoto(InputFile.FromStream(stream, Path.GetFileName(imageUrl)))
                {
                    Caption = text,
                    ParseMode = ParseMode.Html
                };
            }
            else
            {
                media = null;
            }

            if (media != null)
            {
                return await _bot.EditMessageMedia(
                    chatId: chatId,
                    messageId: messageId,
                    media: media,
                    cancellationToken: ct);
            }
        }

        return await _bot.EditMessageText(
            chatId: chatId,
            messageId: messageId,
            text: text,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
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

        Message msg;
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (IsHttpUrl(imageUrl))
            {
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileUrl(imageUrl),
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else if (File.Exists(imageUrl))
            {
                await using var stream = File.OpenRead(imageUrl);
                var file = InputFile.FromStream(stream, Path.GetFileName(imageUrl));
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: file,
                    caption: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
            else
            {
                msg = await _bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
            }
        }
        else
        {
            msg = await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
        }

        var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, audioText, ct);
        return msg;
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

    public async Task<Message> SendWordCardAsync(ChatId chatId, string word, string translation, string? examples, string? imageUrl, CancellationToken ct)
    {
        var text = GenerateWordCardText(word, translation, examples, null);
        Message msg;

        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (IsHttpUrl(imageUrl))
            {
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: new InputFileUrl(imageUrl),
                    caption: text,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            else if (File.Exists(imageUrl))
            {
                await using var stream = File.OpenRead(imageUrl);
                var file = InputFile.FromStream(stream, Path.GetFileName(imageUrl));
                msg = await _bot.SendPhoto(
                    chatId: chatId,
                    photo: file,
                    caption: text,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
            else
            {
                msg = await _bot.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.Html,
                    cancellationToken: ct);
            }
        }
        else
        {
            msg = await _bot.SendMessage(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Html,
                cancellationToken: ct);
        }

        var audioText = string.IsNullOrWhiteSpace(examples) ? word : $"{word}. {examples}";
        await SendVoiceAsync(chatId, audioText, ct);
        return msg;
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

    public async Task<Message> SendText(
        ChatId chatId,
        string text,
        ReplyMarkup replyMarkup,
        CancellationToken ct = default)
    {
        return await _bot.SendMessage(
            chatId: chatId,
            text: text,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup,
            cancellationToken: ct);
    }

    public async Task<Message> SendVoiceAsync(ChatId chatId, string text, CancellationToken ct = default)
    {
        using var stream = await _tts.SynthesizeSpeechAsync(text, _ttsOptions.LanguageCode, _ttsOptions.VoiceName, _ttsOptions.Speed);
        stream.Position = 0;
        var input = InputFile.FromStream(stream, "speech.ogg");
        return await _bot.SendVoice(chatId: chatId, voice: input, cancellationToken: ct);
    }
    public async Task<Message> SendText(
    ChatId chatId,
    string text,
    string imageUrl,
    InlineKeyboardMarkup replyMarkup,
    CancellationToken ct = default)
    {
    if (!string.IsNullOrWhiteSpace(imageUrl))
    {
        if (IsHttpUrl(imageUrl))
        {
            return await _bot.SendPhoto(
                chatId: chatId,
                caption: text,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                photo: InputFile.FromUri(imageUrl),
                cancellationToken: ct);
        }
        else if (File.Exists(imageUrl))
        {
            await using var stream = File.OpenRead(imageUrl);
            var file = InputFile.FromStream(stream, Path.GetFileName(imageUrl));
            return await _bot.SendPhoto(
                chatId: chatId,
                caption: text,
                parseMode: ParseMode.Html,
                replyMarkup: replyMarkup,
                photo: file,
                cancellationToken: ct);
        }
    }

    return await _bot.SendMessage(
        chatId: chatId,
        text: text,
        parseMode: ParseMode.Html,
        replyMarkup: replyMarkup,
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

    private static bool IsHttpUrl(string input)
        => Uri.TryCreate(input, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);


    public async Task SendPhotoWithCaptionAsync(
    ChatId chatId,
    string filePath,
    string caption,
    ReplyMarkup replyMarkup,
    CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var inputFile = InputFile.FromStream(stream, Path.GetFileName(filePath));
        await _bot.SendPhoto(
            chatId: chatId,
            photo: inputFile,
            caption: caption,
            parseMode: ParseMode.Html,
            replyMarkup: replyMarkup,
            cancellationToken: ct
        );
    }


    //private string EscapeHtml(string input) =>
    //    input.Replace("&", "&amp;")
    //         .Replace("<", "&lt;")
    //         .Replace(">", "&gt;")
    //         .Replace("\"", "&quot;")
    //         .Replace("'", "&#39;");
}
