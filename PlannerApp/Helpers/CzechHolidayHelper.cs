namespace PlannerApp.Helpers;

public static class CzechHolidayHelper
{
    public static IReadOnlyDictionary<DateTime, string> GetHolidays(int year)
    {
        // Gregorian computus (Meeus/Jones/Butcher).
        var a = year % 19; var b = year / 100; var c = year % 100;
        var d = b / 4; var e = b % 4; var f = (b + 8) / 25;
        var g = (b - f + 1) / 3; var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4; var k = c % 4; var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var easter = new DateTime(year, (h + l - 7 * m + 114) / 31, (h + l - 7 * m + 114) % 31 + 1);
        return new Dictionary<DateTime, string>
        {
            [new(year, 1, 1)] = "Nový rok", [easter.AddDays(-2)] = "Velký pátek", [easter.AddDays(1)] = "Velikonoční pondělí",
            [new(year, 5, 1)] = "Svátek práce", [new(year, 5, 8)] = "Den vítězství",
            [new(year, 7, 5)] = "Cyril a Metoděj", [new(year, 7, 6)] = "Jan Hus",
            [new(year, 9, 28)] = "Den české státnosti", [new(year, 10, 28)] = "Den vzniku ČSR",
            [new(year, 11, 17)] = "Den svobody", [new(year, 12, 24)] = "Štědrý den",
            [new(year, 12, 25)] = "1. svátek vánoční", [new(year, 12, 26)] = "2. svátek vánoční"
        };
    }
    public static bool IsHoliday(DateTime date) => GetHolidays(date.Year).ContainsKey(date.Date);
    public static string? GetHolidayName(DateTime date) => GetHolidays(date.Year).GetValueOrDefault(date.Date);
    public static IEnumerable<KeyValuePair<DateTime, string>> GetHolidaysInMonth(int year, int month) => GetHolidays(year).Where(day => day.Key.Month == month);
}
