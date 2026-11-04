using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;
using SQLite;
using Xunit;

public sealed class PlannerTests
{
    [Theory]
    [InlineData(2025, 4, 18, 21)]
    [InlineData(2026, 4, 3, 6)]
    [InlineData(2027, 3, 26, 29)]
    [InlineData(2030, 4, 19, 22)]
    public void Easter_holidays_follow_the_selected_year(int year, int month, int friday, int monday)
    {
        Assert.Equal("Velký pátek", CzechHolidayHelper.GetHolidayName(new(year, month, friday)));
        Assert.Equal("Velikonoční pondělí", CzechHolidayHelper.GetHolidayName(new(year, month, monday)));
        Assert.Equal(13, CzechHolidayHelper.GetHolidays(year).Count);
    }

    [Fact]
    public void Year_boundaries_and_leap_days_are_supported()
    {
        Assert.True(CzechHolidayHelper.IsHoliday(new(2027, 1, 1)));
        Assert.False(CzechHolidayHelper.IsHoliday(new(2028, 2, 29)));
        Assert.Equal(new DateTime(2025, 12, 29), DateHelper.GetIsoWeekStart(2026, 1));
        Assert.True(TaborHelper.IsTaborFriday(new(2027, 1, 8)));
    }

    [Theory]
    [InlineData(22, true)] [InlineData(3, true)] [InlineData(12, false)] [InlineData(21, true)] [InlineData(6, false)]
    public void Midnight_blocks_use_exclusive_end_times(int hour, bool expected) =>
        Assert.Equal(expected, ScheduleTime.Contains(TimeSpan.FromHours(hour), TimeSpan.FromHours(21), TimeSpan.FromHours(6)));

    [Fact]
    public void Adjacent_blocks_do_not_overlap_but_midnight_collisions_do()
    {
        var sleep = new ScheduleBlock { TimeFrom = TimeSpan.FromHours(21), TimeTo = TimeSpan.FromHours(6) };
        Assert.False(ScheduleTime.Overlaps(sleep, new() { TimeFrom = TimeSpan.FromHours(6), TimeTo = TimeSpan.FromHours(8) }));
        Assert.True(ScheduleTime.Overlaps(sleep, new() { TimeFrom = TimeSpan.FromHours(5), TimeTo = TimeSpan.FromHours(8) }));
    }

    [Fact]
    public async Task Concurrent_refresh_and_completion_updates_remain_unique()
    {
        var path = Path.Combine(Path.GetTempPath(), $"planner-{Guid.NewGuid()}.db3");
        var db = new DatabaseService(path);
        var days = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => db.GetOrCreateDayLogAsync(new(2027, 1, 4))));
        Assert.Single(days.Select(d => d.Id).Distinct());
        var block = (await db.GetScheduleBlocksForDateAsync(new(2027, 1, 4))).First();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => db.UpsertCompletionAsync(new() { DayLogId = days[0].Id, ScheduleBlockId = block.Id, Status = CompletionStatus.Done })));
        Assert.Single(await db.GetCompletionsForDayAsync(days[0].Id));
        await db.DisposeAsync();
        File.Delete(path);
    }

    [Fact]
    public async Task Backup_roundtrip_preserves_notes_status_and_custom_schedule()
    {
        var db = new DatabaseService(Path.Combine(Path.GetTempPath(), $"planner-{Guid.NewGuid()}.db3"));
        var day = await db.GetOrCreateDayLogAsync(new(2027, 1, 4)); day.Notes = "Žluťoučký den"; await db.UpdateDayLogAsync(day);
        var block = (await db.GetScheduleBlocksForDateAsync(day.Date)).First(); block.ActivityName = "Moje ranní rutina"; await db.SaveScheduleBlockAsync(block);
        await db.UpsertCompletionAsync(new() { DayLogId = day.Id, ScheduleBlockId = block.Id, Status = CompletionStatus.Done });
        var backup = await db.ExportBackupAsync();
        var restored = new DatabaseService(Path.Combine(Path.GetTempPath(), $"planner-{Guid.NewGuid()}.db3"));
        await restored.RestoreBackupAsync(backup);
        Assert.Equal(day.Notes, (await restored.GetOrCreateDayLogAsync(day.Date)).Notes);
        Assert.Equal(CompletionStatus.Done, (await restored.GetCompletionsForDayAsync(day.Id)).Single().Status);
        Assert.Contains(await restored.GetAllScheduleBlocksAsync(), b => b.ActivityName == block.ActivityName);
        await Assert.ThrowsAsync<ArgumentException>(() => restored.RestoreBackupAsync(backup.Replace("\"Version\": 1", "\"Version\": 99")));
        Assert.Equal(day.Notes, (await restored.GetOrCreateDayLogAsync(day.Date)).Notes);
    }

    [Fact]
    public async Task Legacy_database_upgrade_preserves_tabor_history()
    {
        var path = Path.Combine(Path.GetTempPath(), $"planner-{Guid.NewGuid()}.db3");
        var connection = new SQLiteAsyncConnection(path);
        await connection.ExecuteAsync("CREATE TABLE DayLog (Id INTEGER PRIMARY KEY AUTOINCREMENT, Date BIGINT, DayType INTEGER, HolidayName TEXT, Notes TEXT)");
        await connection.CreateTableAsync<ScheduleBlock>(); await connection.CreateTableAsync<BlockCompletion>();
        var old = new ScheduleBlock { DayType = DayType.Tabor, ActivityName = "Legacy Tabor" }; await connection.InsertAsync(old);
        await connection.ExecuteAsync("INSERT INTO DayLog (Date, DayType, Notes) VALUES (?, 0, 'Legacy note')", new DateTime(2026, 1, 1).Ticks);
        var db = new DatabaseService(path); await db.InitializeAsync();
        Assert.Contains(await db.GetAllScheduleBlocksAsync(), b => b.Id == old.Id && b.ActivityName == "Legacy Tabor");
        Assert.Equal("Legacy note", (await db.GetOrCreateDayLogAsync(new(2026, 1, 1))).Notes);
    }
}
