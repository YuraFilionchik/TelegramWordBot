namespace TelegramWordBot.Models;

public class TtsOptions
{
    public string LanguageCode { get; set; } = "en-US";
    public string VoiceName { get; set; } = "en-US-Standard-B";
    public double Speed { get; set; } = 1.0;
}
