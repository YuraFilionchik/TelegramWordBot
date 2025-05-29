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
            int quality = success ? 5 : 2; // 5 — «хорошо вспомнил», 2 — «не вспомнил»

            if (quality < 3)
            {
                prog.Repetition = 0;
                prog.Interval_Days = 1;
            }
            else //TODO include ease factor for Interval_Days calculation
            {
                prog.Repetition++;
                if (prog.Repetition == 1)
                    prog.Interval_Days = 1;
                else if (prog.Repetition == 2)
                    prog.Interval_Days = 6;
                else
                    prog.Interval_Days = (int)Math.Round(prog.Interval_Days * prog.Ease_Factor);
                // Рассчитываем новый EF
                prog.Ease_Factor += 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02);
                if (prog.Ease_Factor < MinEase) prog.Ease_Factor = MinEase;
            }

            prog.Next_Review = DateTime.UtcNow.AddDays(prog.Interval_Days);
            prog.Last_Review = DateTime.UtcNow;
        }
    }

}
