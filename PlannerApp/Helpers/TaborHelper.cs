namespace PlannerApp.Helpers
{
    public static class TaborHelper
    {
        // First Tabor Friday: 2026-01-09. Every 14 days. Fri+Sat+Sun = Tabor period.
        private static readonly DateTime FirstTaborFriday = new(2026, 1, 9);

        private static bool IsTaborFridayDate(DateTime date)
        {
            date = date.Date;
            if (date.DayOfWeek != DayOfWeek.Friday) return false;
            var diff = (date - FirstTaborFriday).TotalDays;
            if (diff < 0) return false;
            return diff % 14 == 0;
        }

        public static bool IsTaborFriday(DateTime date) => IsTaborFridayDate(date.Date);

        public static bool IsTaborSaturday(DateTime date)
        {
            date = date.Date;
            if (date.DayOfWeek != DayOfWeek.Saturday) return false;
            return IsTaborFridayDate(date.AddDays(-1));
        }

        public static bool IsTaborSunday(DateTime date)
        {
            date = date.Date;
            if (date.DayOfWeek != DayOfWeek.Sunday) return false;
            return IsTaborFridayDate(date.AddDays(-2));
        }

        public static bool IsTaborDay(DateTime date)
        {
            date = date.Date;
            return IsTaborFriday(date) || IsTaborSaturday(date) || IsTaborSunday(date);
        }
    }
}
