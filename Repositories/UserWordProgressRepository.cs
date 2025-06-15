using TelegramWordBot;
﻿using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories
{
    public class UserWordProgressRepository
    {
        private readonly IConnectionFactory _factory;

        public UserWordProgressRepository(IConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<UserWordProgress?> GetAsync(Guid userId, Guid wordId)
        {
            using var conn = _factory.CreateConnection();
            return await conn.QueryFirstOrDefaultAsync<UserWordProgress>(
                @"SELECT * 
                  FROM user_word_progress 
                  WHERE user_id = @userId 
                    AND word_id = @wordId",
                new { userId, wordId });
        }

        /// <summary>
        /// Вставляет или обновляет запись прогресса (перезаписывает Repetition, Interval_Hours, Ease_Factor, Next_Review).
        /// </summary>
        public async Task InsertOrUpdateAsync(UserWordProgress progress)
        {
            using var conn = _factory.CreateConnection();
            if (progress.Id == Guid.Empty)
            {
                progress.Id = Guid.NewGuid();
            }

            const string sql = @"
                INSERT INTO user_word_progress
                    (id, user_id, word_id, repetition, interval_hours, ease_factor, next_review, last_review)
                VALUES
                    (@Id, @User_Id, @Word_Id, @Repetition, @Interval_Hours, @Ease_Factor, @Next_Review, @Last_Review)
                ON CONFLICT (user_id, word_id) DO UPDATE
                SET
                    repetition      = EXCLUDED.repetition,
                    interval_hours  = EXCLUDED.interval_hours,
                    ease_factor     = EXCLUDED.ease_factor,
                    next_review     = EXCLUDED.next_review,
                    last_review     = EXCLUDED.last_review";

            await conn.ExecuteAsync(sql, new
            {
                progress.Id,
                progress.User_Id,
                progress.Word_Id,
                progress.Repetition,
                progress.Interval_Hours,
                progress.Ease_Factor,
                progress.Next_Review,
                progress.Last_Review
            });
        }

        /// <summary>
        /// Возвращает все записи прогресса для заданного пользователя.
        /// </summary>
        public async Task<IEnumerable<UserWordProgress>> GetByUserAsync(Guid userId)
        {
            using var conn = _factory.CreateConnection();
            const string sql = @"
                SELECT *
                FROM user_word_progress
                WHERE user_id = @User_Id";
            return await conn.QueryAsync<UserWordProgress>(sql, new { User_Id = userId });
        }
    }
}
