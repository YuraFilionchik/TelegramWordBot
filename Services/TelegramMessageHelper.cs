using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using System;
using System.IO;
using TelegramWordBot.Services.TTS;
using TelegramWordBot.Models;
using Microsoft.Extensions.Localization;

namespace TelegramWordBot.Services;

public class TelegramMessageHelper
{
    private readonly ITelegramBotClient _bot;
    private readonly ITextToSpeechService _tts;
    private readonly TtsOptions _ttsOptions;
    private readonly IStringLocalizer<TelegramMessageHelper> _localizer;

    public TelegramMessageHelper(ITelegramBotClient botClient, ITextToSpeechService tts, TtsOptions options, IStringLocalizer<TelegramMessageHelper> localizer)
    {
        _bot = botClient;
        _tts = tts;
        _ttsOptions = options;
        _localizer = localizer;
    }

    // === Вспомогательный метод для генерации текста карточки слова ===
    public string GenerateWordCardText(string word, string translation, string? example = null, string? category = null)
    {
        var text = $"<b>{EscapeHtml(word)}</b>\n<i>{EscapeHtml(translation)}</i>";

        if (!string.IsNullOrWhiteSpace(example))
            text += string.Format(_localizer["TelegramMessageHelper.WordCardExample"], EscapeHtml(example));

        if (!string.IsNullOrWhiteSpace(category))
            text += string.Format(_localizer["TelegramMessageHelper.WordCardCategory"], EscapeHtml(category));

        return text;
    }
    
    public async Task<Message> SendWordCardWithActions(ChatId chatId, string word, string translation, int wordId, string? example = null, string? category = null, string? imageUrl = null, string? voiceLanguage = null, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.EditButton"], $"edit_{wordId}"),
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.DeleteButton"], $"delete_{wordId}")
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.RepeatButton"], $"repeat_{wordId}"),
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.LearnedButton"], $"learned_{wordId}")
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

        //var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, word, voiceLanguage, ct);
        return msg;
    }

    public async Task<Message> SendWordCardWithEdit(ChatId chatId, string word, string translation, Guid wordId, string? example = null, string? category = null, string? imageUrl = null, string? voiceLanguage = null, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.EditButtonAlternative"], $"edit:{wordId}"));

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

        //var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, word, voiceLanguage, ct);
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
        string? example = null, string? category = null, string? imageUrl = null, string? voiceLanguage = null, CancellationToken ct = default)
    {
        var buttons = new List<InlineKeyboardButton>();

        if (currentIndex > 0)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData(
                text: _localizer["TelegramMessageHelper.BackButton"],
                callbackData: $"prev:{langId}:{currentIndex - 1}"
            ));
        }

        if (currentIndex < totalWords - 1)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData(
                text: _localizer["TelegramMessageHelper.ForwardButton"],
                callbackData: $"next:{langId}:{currentIndex + 1}"
            ));
        }

        var keyboard = new InlineKeyboardMarkup(new[] { buttons.ToArray() });

        // Генерируем текст карточки (можно вынести в общий метод)
        var text = GenerateWordCardText(word, translation, example, category)
                 + string.Format(_localizer["TelegramMessageHelper.WordCardPageIndicator"], currentIndex + 1, totalWords);

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

        //var audioText = string.IsNullOrWhiteSpace(example) ? word : $"{word}. {example}";
        await SendVoiceAsync(chatId, word, voiceLanguage, ct);

        return msg;
    }

    public async Task<Message> SendConfirmationDialog(ChatId chatId, string question, string confirmCallback, string cancelCallback, CancellationToken ct = default)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.ConfirmYesButton"], confirmCallback),
            InlineKeyboardButton.WithCallbackData(_localizer["TelegramMessageHelper.ConfirmNoButton"], cancelCallback)
        }
    });

        return await _bot.SendMessage(
            chatId: chatId,
            text: string.Format(_localizer["TelegramMessageHelper.ConfirmationPrompt"], EscapeHtml(question)),
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

    public async Task<Message> SendWordCardAsync(ChatId chatId, string word, string translation, string? examples, string? imageUrl, string? voiceLanguage, CancellationToken ct)
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

        //var audioText = string.IsNullOrWhiteSpace(examples) ? word : $"{word}. {examples}";
        await SendVoiceAsync(chatId, word, voiceLanguage, ct);
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

    public async Task<Message> SendVoiceAsync(ChatId chatId, string text, string? voiceLanguage = null, CancellationToken ct = default)
    {
        var lang = voiceLanguage ?? _ttsOptions.LanguageCode;

        var dir = Path.Combine(AppContext.BaseDirectory, "speech");
        Directory.CreateDirectory(dir);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(lang + "|" + text)))
            .ToLowerInvariant();
        var filePath = Path.Combine(dir, $"{hash}.ogg");

        if (!File.Exists(filePath))
        {
            await using var synth = await _tts.SynthesizeSpeechAsync(text, lang, _ttsOptions.Speed);
            synth.Position = 0;
            await using var fs = File.Create(filePath);
            await synth.CopyToAsync(fs, ct);
        }

        await using var voiceStream = File.OpenRead(filePath);
        var input = InputFile.FromStream(voiceStream, Path.GetFileName(filePath));
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
        var text = string.Format(_localizer["TelegramMessageHelper.ErrorMessage"], EscapeHtml(message));
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }

    public async Task SendSuccessAsync(ChatId chatId, string message, CancellationToken ct)
    {
        var text = string.Format(_localizer["TelegramMessageHelper.SuccessMessage"], EscapeHtml(message));
        await _bot.SendMessage(chatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
    }
    public async Task SendInfoAsync(ChatId chatId, string message, CancellationToken ct)
    {
        var text = string.Format(_localizer["TelegramMessageHelper.InfoMessage"], EscapeHtml(message));
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
