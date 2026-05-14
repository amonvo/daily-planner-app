using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.ViewModels
{
    public partial class MonthDayCell : ObservableObject
    {
        public DateTime Date { get; set; }
        public int Day { get; set; }
        public string DayLabel => Day == 0 ? string.Empty : Day.ToString();
        public bool IsInMonth { get; set; }
        public bool IsTabor { get; set; }
        public string? HolidayName { get; set; }
        public double Progress { get; set; }
        public bool HasData { get; set; }
        public string DotColor { get; set; } = "#CBD5E1";
        public Color CellBackground { get; set; } = Colors.Transparent;
    }

    public partial class MonthViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;

        public MonthViewModel(DatabaseService db)
        {
            _db = db;
            Title = "Mìsíc";
            var today = DateTime.Today;
            month = today.Month;
            year = today.Year;
        }

        [ObservableProperty]
        private int month;

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string monthLabel = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MonthDayCell> cells = new();

        [ObservableProperty]
        private string summaryLabel = string.Empty;

        [ObservableProperty]
        private string streakLabel = string.Empty;

        [ObservableProperty]
        private string bestWeekLabel = string.Empty;

        [ObservableProperty]
        private ObservableCollection<string> holidays = new();

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                MonthLabel = $"{DateHelper.FormatCzechMonthName(Month)} {Year}";

                var firstDay = new DateTime(Year, Month, 1);
                var daysInMonth = DateTime.DaysInMonth(Year, Month);
                var lastDay = new DateTime(Year, Month, daysInMonth);

                // Start grid on Monday
                int startDow = (int)firstDay.DayOfWeek;
                if (startDow == 0) startDow = 7;
                var gridStart = firstDay.AddDays(-(startDow - 1));

                int endDow = (int)lastDay.DayOfWeek;
                if (endDow == 0) endDow = 7;
                var gridEnd = lastDay.AddDays(7 - endDow);
                int totalCells = (int)(gridEnd - gridStart).TotalDays + 1;

                var monthLogs = await _db.GetMonthLogsAsync(Month, Year);
                var allBlocks = await _db.GetAllScheduleBlocksAsync();
                var completions = await _db.GetCompletionsForDaysAsync(monthLogs.Select(l => l.Id).ToList());

                var newCells = new ObservableCollection<MonthDayCell>();
                int monthDone = 0, monthReq = 0;
                var perDayProgress = new Dictionary<DateTime, double>();

                for (int i = 0; i < totalCells; i++)
                {
                    var date = gridStart.AddDays(i);
                    var inMonth = date.Month == Month;
                    var isTabor = TaborHelper.IsTaborDay(date);
                    var holidayName = CzechHolidayHelper.GetHolidayName(date);

                    double progress = 0;
                    bool hasData = false;
                    string dotColor = "#CBD5E1";

                    if (inMonth)
                    {
                        var log = monthLogs.FirstOrDefault(l => l.Date.Date == date.Date);
                        var scheduleDay = DateHelper.GetScheduleDayType(date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired).ToList();
                        var dayCompletions = log is null ? new List<BlockCompletion>() :
                            completions.Where(c => c.DayLogId == log.Id).ToList();
                        var done = required.Count(b => dayCompletions.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                        progress = required.Count == 0 ? 0 : (double)done / required.Count;
                        hasData = dayCompletions.Count > 0;

                        if (hasData)
                        {
                            if (progress > 0.75) dotColor = "#16A34A";
                            else if (progress >= 0.5) dotColor = "#EAB308";
                            else dotColor = "#DC2626";
                        }

                        monthDone += done;
                        monthReq += required.Count;
                        perDayProgress[date.Date] = hasData ? progress : -1;
                    }

                    newCells.Add(new MonthDayCell
                    {
                        Date = date,
                        Day = date.Day,
                        IsInMonth = inMonth,
                        IsTabor = isTabor,
                        HolidayName = holidayName,
                        Progress = progress,
                        HasData = hasData,
                        DotColor = dotColor,
                        CellBackground = !inMonth ? Color.FromArgb("#F1F5F9") :
                            isTabor ? Color.FromArgb("#DBEAFE") :
                            holidayName is not null ? Color.FromArgb("#FEF3C7") :
                            Colors.Transparent
                    });
                }

                Cells = newCells;

                var monthPercent = monthReq == 0 ? 0 : (int)Math.Round((double)monthDone / monthReq * 100);
                SummaryLabel = $"Celkem: {monthDone} / {monthReq} ({monthPercent}%)";

                // Streak: consecutive days up to today with progress > 0.75
                int streak = 0;
                var today = DateTime.Today;
                for (var d = today; d.Month == Month && d.Year == Year; d = d.AddDays(-1))
                {
                    if (perDayProgress.TryGetValue(d, out var p) && p > 0.75) streak++;
                    else break;
                }
                StreakLabel = $"Aktuální série: {streak} dní";

                // Best week
                var weeksInMonth = monthLogs.GroupBy(l => DateHelper.GetIsoWeekNumber(l.Date)).ToList();
                int bestWeek = 0; double bestPct = -1;
                foreach (var grp in weeksInMonth)
                {
                    int wDone = 0, wReq = 0;
                    foreach (var log in grp)
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired).ToList();
                        var dayCompletions = completions.Where(c => c.DayLogId == log.Id).ToList();
                        wDone += required.Count(b => dayCompletions.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                        wReq += required.Count;
                    }
                    var pct = wReq == 0 ? 0 : (double)wDone / wReq;
                    if (pct > bestPct) { bestPct = pct; bestWeek = grp.Key; }
                }
                BestWeekLabel = bestWeek == 0 ? "Nejlepší týden: —" : $"Nejlepší týden: {bestWeek} ({(int)Math.Round(bestPct * 100)}%)";

                var hol = new ObservableCollection<string>();
                foreach (var kv in CzechHolidayHelper.GetHolidaysInMonth(Year, Month))
                    hol.Add($"{kv.Key:d. M.} – {kv.Value}");
                Holidays = hol;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MonthLoad error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PrevMonthAsync()
        {
            var d = new DateTime(Year, Month, 1).AddMonths(-1);
            Month = d.Month;
            Year = d.Year;
            await LoadAsync();
        }

        [RelayCommand]
        private async Task NextMonthAsync()
        {
            var d = new DateTime(Year, Month, 1).AddMonths(1);
            Month = d.Month;
            Year = d.Year;
            await LoadAsync();
        }
    }
}
