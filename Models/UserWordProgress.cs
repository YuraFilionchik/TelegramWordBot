namespace TelegramWordBot.Models;

public class UserWordProgress
{
    public Guid Id { get; set; }
    public Guid User_Id { get; set; }
    public Guid Word_Id { get; set; }
    public DateTime? Last_Review { get; set; }
    public int Count_Total_View { get; set; } = 0;
    public int Count_Plus { get; set; } = 0;
    public int Count_Minus { get; set; } =0;
    public int Progress { get; set; } = 0;
    public int Repetition { get; set; }        // число подряд успешных повторений
    public int Interval_Hours { get; set; }          // текущий интервал в днях
    public double Ease_Factor { get; set; }     // «коэффициент лёгкости» (обычно ≥1.3)
    public DateTime Next_Review { get; set; }   // дата следующего показа
}
