namespace PlannerApp.Helpers;

public sealed record TrendPoint(DateTime Date, string Label, double? Progress, int Done, int Required)
{
    public string ValueLabel => Progress is null ? "Bez záznamu" : $"{Progress:P0}";
}

public static class ProgressInsights
{
    public static int LongestStreak(IEnumerable<TrendPoint> points)
    {
        var longest = 0;
        var current = 0;
        DateTime? previous = null;
        foreach (var point in points.OrderBy(point => point.Date))
        {
            if (previous is null || point.Date.Date != previous.Value.AddDays(1)) current = 0;
            current = point.Progress >= .75 ? current + 1 : 0;
            longest = Math.Max(longest, current);
            previous = point.Date.Date;
        }
        return longest;
    }
}
