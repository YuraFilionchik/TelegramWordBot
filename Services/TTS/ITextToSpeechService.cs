namespace TelegramWordBot.Services.TTS;

public interface ITextToSpeechService
{
    /// <summary>
    /// Synthesize speech for the specified text using a voice for the given language.
    /// The <paramref name="language"/> parameter represents the name or code of
    /// the language to be used for speech generation.
    /// </summary>
    Task<Stream> SynthesizeSpeechAsync(string text, string language, double speed);
}
