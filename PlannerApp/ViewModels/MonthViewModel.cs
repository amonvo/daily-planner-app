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
        public bool IsSpecialDay { get; set; }
    }

    public partial class MonthCategoryStat : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public int Done { get; set; }
        public int Total { get; set; }
        public double Progress { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#7C3AED";
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
        private string longestStreakLabel = string.Empty;

        [ObservableProperty]
        private string bestWeekLabel = string.Empty;

        [ObservableProperty]
        private string worstWeekLabel = string.Empty;

        [ObservableProperty]
        private string specialDaysLabel = string.Empty;

        [ObservableProperty]
        private double monthTotalProgress;

        [ObservableProperty]
        private ObservableCollection<MonthCategoryStat> categoryStats = new();

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
                int specialDaysCount = 0;

                for (int i = 0; i < totalCells; i++)
                {
                    var date = gridStart.AddDays(i);
                    var inMonth = date.Month == Month;
                    var isTabor = TaborHelper.IsTaborDay(date);
                    var holidayName = CzechHolidayHelper.GetHolidayName(date);

                    double progress = 0;
                    bool hasData = false;
                    string dotColor = "#CBD5E1";
                    bool isSpecialDay = false;

                    if (inMonth)
                    {
                        var log = monthLogs.FirstOrDefault(l => l.Date.Date == date.Date);
                        isSpecialDay = log?.IsSpecialDay ?? false;
                        if (isSpecialDay) specialDaysCount++;

                        if (!isSpecialDay)
                        {
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
                        IsSpecialDay = isSpecialDay,
                        CellBackground = !inMonth ? Color.FromArgb("#F1F5F9") :
                            isSpecialDay ? Color.FromArgb("#FEF3C7") :
                            isTabor ? Color.FromArgb("#DBEAFE") :
                            holidayName is not null ? Color.FromArgb("#FEF3C7") :
                            Colors.Transparent
                    });
                }

                Cells = newCells;

                var monthPercent = monthReq == 0 ? 0 : (double)monthDone / monthReq;
                MonthTotalProgress = monthPercent;
                SummaryLabel = $"Celkem: {monthDone} / {monthReq} ({(int)Math.Round(monthPercent * 100)}%)";
                SpecialDaysLabel = specialDaysCount == 0 ? "Žádné speciální dny" :
                    $"{specialDaysCount} {(specialDaysCount == 1 ? "den oznaèen" : specialDaysCount < 5 ? "dny oznaèeny" : "dní oznaèeno")} jako speciální";

                // Streak (current)
                int streak = 0;
                var today = DateTime.Today;
                for (var d = today; d.Month == Month && d.Year == Year; d = d.AddDays(-1))
                {
                    if (perDayProgress.TryGetValue(d, out var p) && p > 0.75) streak++;
                    else break;
                }
                StreakLabel = $"Aktuální série: {streak} dní";

                // Longest streak in month
                int longest = 0, cur = 0;
                for (int d = 1; d <= daysInMonth; d++)
                {
                    var date = new DateTime(Year, Month, d);
                    var log = monthLogs.FirstOrDefault(l => l.Date.Date == date);
                    if (log?.IsSpecialDay == true) { cur = 0; continue; }
                    if (perDayProgress.TryGetValue(date, out var p2) && p2 > 0.75)
                    {
                        cur++;
                        if (cur > longest) longest = cur;
                    }
                    else cur = 0;
                }
                LongestStreakLabel = $"Nejdelší série: {longest} dní";

                // Best and worst week
                var weeksInMonth = monthLogs
                    .Where(l => !l.IsSpecialDay)
                    .GroupBy(l => DateHelper.GetIsoWeekNumber(l.Date)).ToList();
                int bestWeek = 0, worstWeek = 0;
                double bestPct = -1, worstPct = 2;
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
                    if (pct < worstPct) { worstPct = pct; worstWeek = grp.Key; }
                }
                BestWeekLabel = bestWeek == 0 ? "Nejlepší týden: –" : $"Nejlepší týden: {bestWeek} ({(int)Math.Round(bestPct * 100)}%)";
                WorstWeekLabel = worstWeek == 0 ? "Nejslabší týden: –" : $"Nejslabší týden: {worstWeek} ({(int)Math.Round(worstPct * 100)}%)";

                // Per-category stats
                var catStats = new ObservableCollection<MonthCategoryStat>();
                var requiredCategories = new[] { Category.DotNet, Category.Project, Category.Reading, Category.Running };
                foreach (var cat in requiredCategories)
                {
                    int catDone = 0, catTotal = 0;
                    var eligibleLogs = monthLogs.Where(l => !l.IsSpecialDay).ToList();
                    foreach (var log in eligibleLogs)
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired && b.Category == cat).ToList();
                        if (required.Count == 0) continue;
                        catTotal++;
                        var dayCompletions = completions.Where(c => c.DayLogId == log.Id).ToList();
                        if (required.Any(b => dayCompletions.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done)))
                            catDone++;
                    }
                    if (catTotal == 0) continue;
                    var catProgress = (double)catDone / catTotal;
                    catStats.Add(new MonthCategoryStat
                    {
                        Name = WeekViewModel.GetCategoryCzech(cat),
                        Done = catDone,
                        Total = catTotal,
                        Progress = catProgress,
                        Label = $"{catDone} / {catTotal} dní  {(int)Math.Round(catProgress * 100)}%",
                        Color = BlockViewModel.GetCategoryColor(cat)
                    });
                }
                CategoryStats = catStats;

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
