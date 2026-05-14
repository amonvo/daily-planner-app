namespace PlannerApp.Helpers
{
    public static class CzechHolidayHelper
    {
        public static readonly IReadOnlyDictionary<DateTime, string> Holidays2026 =
            new Dictionary<DateTime, string>
            {
                { new DateTime(2026, 1, 1), "Nový rok" },
                { new DateTime(2026, 4, 3), "Velký pátek" },
                { new DateTime(2026, 4, 6), "Velikonoèní pondìlí" },
                { new DateTime(2026, 5, 1), "Svátek práce" },
                { new DateTime(2026, 5, 8), "Den vítìzství" },
                { new DateTime(2026, 7, 5), "Cyril a Metodìj" },
                { new DateTime(2026, 7, 6), "Jan Hus" },
                { new DateTime(2026, 9, 28), "Den èeské státnosti" },
                { new DateTime(2026, 10, 28), "Den vzniku ÈSR" },
                { new DateTime(2026, 11, 17), "Den svobody" },
                { new DateTime(2026, 12, 24), "Štìdrý den" },
                { new DateTime(2026, 12, 25), "1. svátek vánoèní" },
                { new DateTime(2026, 12, 26), "2. svátek vánoèní" }
            };

        public static bool IsHoliday(DateTime date) =>
            Holidays2026.ContainsKey(date.Date);

        public static string? GetHolidayName(DateTime date) =>
            Holidays2026.TryGetValue(date.Date, out var name) ? name : null;

        public static IEnumerable<KeyValuePair<DateTime, string>> GetHolidaysInMonth(int year, int month) =>
            Holidays2026.Where(h => h.Key.Year == year && h.Key.Month == month);
    }
}
