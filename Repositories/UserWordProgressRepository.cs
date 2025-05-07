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

        var existing = await GetAsync(progress.User_Id, progress.Word_Id);

        if (existing == null)
        {
            progress.Id = Guid.NewGuid();
            progress.Count_Total_View = 1;
            progress.Count_Plus = success ? 1 : 0;
            progress.Count_Minus = success ? 0 : 1;
            progress.Progress = success ? 10 : 0;
            progress.Last_Review = DateTime.UtcNow;

            await conn.ExecuteAsync(@"
                INSERT INTO user_word_progress 
                (id, user_id, word_id, last_review, count_total_view, count_plus, count_minus, progress) 
                VALUES (@Id, @User_Id, @Word_Id, @Last_Review, @Count_Total_View, @Count_Plus, @Count_Minus, @Progress)", progress);
        }
        else
        {
            existing.Count_Total_View++;
            existing.Last_Review = DateTime.UtcNow;
            if (success) { existing.Count_Plus++; existing.Progress += 10; }
            else { existing.Count_Minus++; existing.Progress -= 5; }

            await conn.ExecuteAsync(@"
                UPDATE user_word_progress SET 
                    last_review = @Last_Review,
                    count_total_view = @Count_Total_View,
                    count_plus = @Count_Plus,
                    count_minus = @Count_Minus,
                    progress = @Progress
                WHERE user_id = @User_Id AND word_id = @Word_Id", existing);
        }

    }


}
