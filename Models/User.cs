namespace TelegramWordBot.Models;

public class User
{
    public Guid Id { get; set; }
    public long Telegram_Id { get; set; }
    public string Native_Language { get; set; } = "Russian";
    public string? Current_Language { get; set; }
    public bool Prefer_Multiple_Choice { get; set; } = false;

    public string? First_Name { get; set; }
    public string? Last_Name { get; set; }
    public bool Is_Premium {  get; set; }=false;
    public string? User_Name { get; set; }
    public DateTime Last_Seen { get; set; }
    public bool Receive_Reminders { get; set; } = true;
}
