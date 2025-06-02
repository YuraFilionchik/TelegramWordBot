using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TelegramWordBot.Models;

namespace TelegramWordBot.Services
{
    public class SpacedRepetitionService
    {
        private const double MinEase = 1.3;

        /// <summary>
        /// Обновляет прогресс по алгоритму SM-2.
        /// </summary>
        public void UpdateProgress(UserWordProgress prog, bool success)
        {
            // Качество ответа: 5 — «хорошо вспомнил», 0 — «не вспомнил»
            int quality = success ? 5 : 0;

            if (quality < 3)
            {
                // Если не вспомнил — сбрасываем повторения и ставим следующий показ через 1 час
                prog.Repetition --;
                if (prog.Repetition < 0 ) prog.Repetition = 0; 
                prog.Interval_Hours = 1;
            }
            else
            {
                prog.Repetition++;

                // Начальные интервалы в часах (более частые повторы внутри дня)
                if (prog.Repetition == 1)
                {
                    prog.Interval_Hours = 2;   // через 1 час
                }
                else if (prog.Repetition == 2)
                {
                    prog.Interval_Hours = 6;   // через 6 часов
                }
                else
                {
                    // Для последующих повторов умножаем предыдущий интервал (в часах) на EF
                    prog.Interval_Hours = (int)Math.Round(prog.Interval_Hours * prog.Ease_Factor);
                }

                // Пересчёт Ease Factor по SM-2 (минимум MinEase, обычно 1.3)
                prog.Ease_Factor = prog.Ease_Factor
                                   + (0.15 - (5 - quality) * (0.08 + (5 - quality) * 0.03));
                if (prog.Ease_Factor < MinEase)
                    prog.Ease_Factor = MinEase;
            }

            // Обновляем дату последнего показа и рассчитываем следующий по интервалу в часах
            prog.Last_Review = DateTime.UtcNow;
            prog.Next_Review = DateTime.UtcNow.AddHours(prog.Interval_Hours);
        }

    }

}
