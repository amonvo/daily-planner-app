using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;
using Xunit;

public class InsightTests
{
    [Fact]
    public void Missing_calendar_days_break_a_streak()
    {
        var points = new[] {
            new TrendPoint(new(2026, 1, 1), "", 1, 4, 4),
            new TrendPoint(new(2026, 1, 3), "", 1, 4, 4),
            new TrendPoint(new(2026, 1, 4), "", .75, 3, 4)
        };
        Assert.Equal(2, ProgressInsights.LongestStreak(points));
    }
    [Fact]
    public void Empty_and_special_days_break_a_streak()
    {
        var start = new DateTime(2026, 1, 1);
        Assert.Equal(1, ProgressInsights.LongestStreak(new[] { new TrendPoint(start, "", 1, 1, 1), new(start.AddDays(1), "", null, 0, 0), new(start.AddDays(2), "", 1, 1, 1) }));
    }
    [Fact]
    public async Task Insights_distinguish_missing_data_from_zero_progress_without_creating_logs()
    {
        var path = Path.Combine(Path.GetTempPath(), $"insights-{Guid.NewGuid()}.db3");
        await using var db = new DatabaseService(path);
        var start = new DateTime(2026, 1, 5);
        var log = await db.GetOrCreateDayLogAsync(start);
        var block = (await db.GetScheduleBlocksForDateAsync(start)).First(block => block.IsRequired);
        await db.UpsertCompletionAsync(new() { DayLogId = log.Id, ScheduleBlockId = block.Id, Status = CompletionStatus.Skipped });
        var insights = await new InsightService(db).GetDaysAsync(start, start.AddDays(2));
        Assert.Equal(0d, insights[0].Progress);
        Assert.Null(insights[1].Progress);
        Assert.Null(insights[2].Progress);
        Assert.Single(await db.GetLogsAsync(start, start.AddDays(2)));
    }
}
