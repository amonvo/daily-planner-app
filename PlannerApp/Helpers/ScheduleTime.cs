using PlannerApp.Models;
namespace PlannerApp.Helpers;
public static class ScheduleTime
{
    public static bool Contains(TimeSpan now, TimeSpan from, TimeSpan to) =>
        from < to ? now >= from && now < to : now >= from || now < to;

    public static bool Overlaps(ScheduleBlock first, ScheduleBlock second)
    {
        static IEnumerable<(TimeSpan Start, TimeSpan End)> Segments(ScheduleBlock block)
        {
            if (block.TimeFrom < block.TimeTo) yield return (block.TimeFrom, block.TimeTo);
            else { yield return (block.TimeFrom, TimeSpan.FromDays(1)); yield return (TimeSpan.Zero, block.TimeTo); }
        }
        return Segments(first).Any(left => Segments(second).Any(right => left.Start < right.End && right.Start < left.End));
    }
}
