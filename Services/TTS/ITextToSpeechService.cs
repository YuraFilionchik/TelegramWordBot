namespace TelegramWordBot.Services.TTS;

public interface ITextToSpeechService
{
    Task<Stream> SynthesizeSpeechAsync(string text, string languageCode, string voiceName, double speed);
}
