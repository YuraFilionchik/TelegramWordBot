using System;
using TelegramWordBot.Models;
using TelegramWordBot.Services;
using Xunit;

namespace TelegramWordBot.Tests;

public class UpdateProgressTests
{
    [Fact]
    public void UpdateProgress_Success_IncreasesRepetitionAndInterval()
    {
        var service = new SpacedRepetitionService();
        var prog = new UserWordProgress
        {
            Repetition = 0,
            Interval_Hours = 0,
            Ease_Factor = 2.5
        };

        var before = DateTime.UtcNow;
        service.UpdateProgress(prog, true);
        var after = DateTime.UtcNow;

        Assert.Equal(1, prog.Repetition);
        Assert.Equal(2, prog.Interval_Hours);
        Assert.InRange(prog.Next_Review, before.AddHours(prog.Interval_Hours), after.AddHours(prog.Interval_Hours + 0.001));
    }

    [Fact]
    public void UpdateProgress_Failure_ResetsIntervalAndDecrementsRepetition()
    {
        var service = new SpacedRepetitionService();
        var prog = new UserWordProgress
        {
            Repetition = 2,
            Interval_Hours = 6,
            Ease_Factor = 2.5
        };

        var before = DateTime.UtcNow;
        service.UpdateProgress(prog, false);
        var after = DateTime.UtcNow;

        Assert.Equal(1, prog.Repetition);
        Assert.Equal(1, prog.Interval_Hours);
        Assert.InRange(prog.Next_Review, before.AddHours(prog.Interval_Hours), after.AddHours(prog.Interval_Hours + 0.001));
    }
}
