namespace PlannerApp.Models
{
    public enum DayType
    {
        Workday = 0,
        Weekend = 1,
        Holiday = 2,
        Tabor = 3
    }

    public enum Category
    {
        Sleep = 0,
        Routine = 1,
        Work = 2,
        Commute = 3,
        DotNet = 4,
        Project = 5,
        Reading = 6,
        Running = 7,
        FreeTime = 8
    }

    public enum CompletionStatus
    {
        NotDone = 0,
        Done = 1,
        Skipped = 2
    }
}
