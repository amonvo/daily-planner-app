using PlannerApp.Helpers;
using PlannerApp.Models;

namespace PlannerApp.Services;

public sealed class InsightService(DatabaseService database)
{
    public async Task<List<TrendPoint>> GetDaysAsync(DateTime from, DateTime to)
    {
        var logs = await database.GetLogsAsync(from, to);
        var completions = await database.GetCompletionsForDaysAsync(logs.Select(day => day.Id).ToList());
        var points = new List<TrendPoint>();
        for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
        {
            var log = logs.FirstOrDefault(day => day.Date == date);
            var done = 0;
            var required = 0;
            double? progress = null;
            if (log is not null && !log.IsSpecialDay && date <= DateTime.Today)
            {
                var blocks = (await database.GetScheduleBlocksForDateAsync(date)).Where(block => block.IsRequired).ToList();
                var entries = completions.Where(entry => entry.DayLogId == log.Id).ToList();
                required = blocks.Count;
                done = blocks.Count(block => entries.Any(entry => entry.ScheduleBlockId == block.Id && entry.Status == CompletionStatus.Done));
                if (required > 0 && entries.Count > 0) progress = (double)done / required;
            }
            points.Add(new TrendPoint(date, DateHelper.FormatCzechDayName(date)[..2], progress, done, required));
        }
        return points;
    }
}
