using Dapper;
using TelegramWordBot.Models;

namespace TelegramWordBot.Repositories;

public class UserWordProgressRepository
{
    private readonly DbConnectionFactory _factory;

    public UserWordProgressRepository(DbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<UserWordProgress?> GetAsync(Guid userId, Guid wordId)
    {
        using var conn = _factory.CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<UserWordProgress>(
            "SELECT * FROM user_word_progress WHERE user_id = @userId AND word_id = @wordId",
            new { userId, wordId });
    }

    public async Task InsertOrUpdateAsync(UserWordProgress progress, bool success)
    {
        using var conn = _factory.CreateConnection();

        var existing = await GetAsync(progress.UserId, progress.WordId);

        if (existing == null)
        {
            progress.Id = Guid.NewGuid();
            progress.CountTotalView = 1;
            progress.CountPlus = success ? 1 : 0;
            progress.CountMinus = success ? 0 : 1;
            progress.Progress = success ? 10 : 0;
            progress.LastReview = DateTime.UtcNow;

            await conn.ExecuteAsync(@"
                INSERT INTO user_word_progress 
                (id, user_id, word_id, last_review, count_total_view, count_plus, count_minus, progress) 
                VALUES (@Id, @UserId, @WordId, @LastReview, @CountTotalView, @CountPlus, @CountMinus, @Progress)", progress);
        }
        else
        {
            existing.CountTotalView++;
            existing.LastReview = DateTime.UtcNow;
            if (success) { existing.CountPlus++; existing.Progress += 10; }
            else { existing.CountMinus++; existing.Progress -= 5; }

            await conn.ExecuteAsync(@"
                UPDATE user_word_progress SET 
                    last_review = @LastReview,
                    count_total_view = @CountTotalView,
                    count_plus = @CountPlus,
                    count_minus = @CountMinus,
                    progress = @Progress
                WHERE user_id = @UserId AND word_id = @WordId", existing);
        }

    }


}
