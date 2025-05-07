namespace TelegramWordBot.Models;

public class UserWord
{
    public Guid User_Id { get; set; }
    public Guid Word_Id { get; set; }
    public Guid? Translation_Id { get; set; }
    

}
