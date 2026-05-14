using SQLite;

namespace PlannerApp.Models
{
    public class DayLog
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public DateTime Date { get; set; }

        public DayType DayType { get; set; }

        public string? HolidayName { get; set; }

        public string? Notes { get; set; }
    }
}
