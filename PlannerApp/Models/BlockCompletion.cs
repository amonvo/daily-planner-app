using SQLite;

namespace PlannerApp.Models
{
    public class BlockCompletion
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int DayLogId { get; set; }

        [Indexed]
        public int ScheduleBlockId { get; set; }

        public CompletionStatus Status { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
