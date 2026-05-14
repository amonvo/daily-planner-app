namespace PlannerApp.Helpers
{
    public static class TaborHelper
    {
        // First Tabor Friday: 2026-01-09. Every 14 days. Fri+Sat+Sun = Tabor period.
        private static readonly DateTime FirstTaborFriday = new(2026, 1, 9);

        public static bool IsTaborDay(DateTime date)
        {
            date = date.Date;
            // Find the closest Friday <= date and check if it's Fri/Sat/Sun within a tabor weekend
            DateTime friday = date.DayOfWeek switch
            {
                DayOfWeek.Friday => date,
                DayOfWeek.Saturday => date.AddDays(-1),
                DayOfWeek.Sunday => date.AddDays(-2),
                _ => DateTime.MinValue
            };

            if (friday == DateTime.MinValue)
                return false;

            var diff = (friday - FirstTaborFriday).TotalDays;
            if (diff < 0)
                return false;

            return diff % 14 == 0;
        }
    }
}
