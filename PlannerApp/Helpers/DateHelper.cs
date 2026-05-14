using System.Globalization;
using PlannerApp.Models;

namespace PlannerApp.Helpers
{
    public static class DateHelper
    {
        private static readonly CultureInfo CzechCulture = new("cs-CZ");

        public static int GetIsoWeekNumber(DateTime date) =>
            ISOWeek.GetWeekOfYear(date);

        public static int GetIsoWeekYear(DateTime date) =>
            ISOWeek.GetYear(date);

        public static DayType DetectDayType(DateTime date)
        {
            date = date.Date;
            if (TaborHelper.IsTaborDay(date))
                return DayType.Tabor;
            if (CzechHolidayHelper.IsHoliday(date))
                return DayType.Holiday;
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                return DayType.Weekend;
            return DayType.Workday;
        }

        /// <summary>
        /// Returns the DayType that should be used to look up ScheduleBlocks.
        /// Holiday days fall back to Weekend schedule.
        /// </summary>
        public static DayType GetScheduleDayType(DateTime date)
        {
            var dt = DetectDayType(date);
            return dt == DayType.Holiday ? DayType.Weekend : dt;
        }

        public static string FormatLongCzechDate(DateTime date) =>
            CapitalizeFirst(date.ToString("dddd d. MMMM yyyy", CzechCulture));

        public static string FormatShortCzechDate(DateTime date) =>
            date.ToString("d. M. yyyy", CzechCulture);

        public static string FormatCzechDayName(DateTime date) =>
            CapitalizeFirst(date.ToString("dddd", CzechCulture));

        public static string FormatCzechMonthName(int month) =>
            CapitalizeFirst(new DateTime(2000, month, 1).ToString("MMMM", CzechCulture));

        public static string GetDayTypeLabel(DayType dayType) => dayType switch
        {
            DayType.Workday => "PRACOVNÍ DEN",
            DayType.Weekend => "VÍKEND",
            DayType.Holiday => "SVÁTEK",
            DayType.Tabor => "TÁBOR",
            _ => string.Empty
        };

        public static DateTime GetIsoWeekStart(int year, int week)
        {
            var jan4 = new DateTime(year, 1, 4);
            var jan4DayOfWeek = (int)jan4.DayOfWeek;
            if (jan4DayOfWeek == 0) jan4DayOfWeek = 7; // Sunday -> 7
            var week1Monday = jan4.AddDays(1 - jan4DayOfWeek);
            return week1Monday.AddDays((week - 1) * 7);
        }

        public static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0], CzechCulture) + s.Substring(1);
        }

        public static string FormatTimeRange(TimeSpan from, TimeSpan to) =>
            $"{from:hh\\:mm} – {to:hh\\:mm}";
    }
}
