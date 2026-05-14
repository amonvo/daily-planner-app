using SQLite;

namespace PlannerApp.Models
{
    public class ScheduleBlock
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DayType DayType { get; set; }

        public TimeSpan TimeFrom { get; set; }

        public TimeSpan TimeTo { get; set; }

        public string ActivityName { get; set; } = string.Empty;

        public Category Category { get; set; }

        public bool IsRequired { get; set; }

        public string NotificationMessage { get; set; } = string.Empty;
    }
}
