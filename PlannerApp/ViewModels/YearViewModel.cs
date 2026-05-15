using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.ViewModels
{
    public partial class MonthTile : ObservableObject
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PercentLabel { get; set; } = "–";
        public Color Background { get; set; } = Color.FromArgb("#E2E8F0");
    }

    public partial class ActivityStat : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#7C3AED";
        public double Progress { get; set; }
    }

    public partial class YearViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;

        public YearViewModel(DatabaseService db)
        {
            _db = db;
            Title = "Rok";
            year = DateTime.Today.Year;
            if (year < 2026) year = 2026;
        }

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string yearLabel = string.Empty;

        [ObservableProperty]
        private ObservableCollection<MonthTile> tiles = new();

        [ObservableProperty]
        private string daysWithDataLabel = string.Empty;

        [ObservableProperty]
        private string longestStreakLabel = string.Empty;

        [ObservableProperty]
        private string bestMonthLabel = string.Empty;

        [ObservableProperty]
        private string worstMonthLabel = string.Empty;

        [ObservableProperty]
        private string yearTotalLabel = string.Empty;

        [ObservableProperty]
        private string totalBlocksLabel = string.Empty;

        [ObservableProperty]
        private string specialDaysLabel = string.Empty;

        [ObservableProperty]
        private string taborWeekendsLabel = string.Empty;

        [ObservableProperty]
        private double yearTotalProgress;

        [ObservableProperty]
        private ObservableCollection<ActivityStat> activityStats = new();

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                YearLabel = $"Rok {Year}";

                var yearLogs = await _db.GetYearLogsAsync(Year);
                var allBlocks = await _db.GetAllScheduleBlocksAsync();
                var completions = await _db.GetCompletionsForDaysAsync(yearLogs.Select(l => l.Id).ToList());

                var newTiles = new ObservableCollection<MonthTile>();
                var today = DateTime.Today;
                int bestMonth = 0, worstMonth = 0;
                double bestPct = -1, worstPct = 2;
                int yearTotalDone = 0, yearTotalReq = 0;
                int specialDaysCount = yearLogs.Count(l => l.IsSpecialDay);

                for (int m = 1; m <= 12; m++)
                {
                    var monthLogs = yearLogs.Where(l => l.Date.Month == m && !l.IsSpecialDay).ToList();
                    int done = 0, req = 0;
                    foreach (var log in monthLogs)
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired).ToList();
                        var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                        var d2 = required.Count(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                        done += d2;
                        req += required.Count;
                    }

                    yearTotalDone += done;
                    yearTotalReq += req;

                    string percentLabel;
                    Color bg;
                    var future = new DateTime(Year, m, 1) > new DateTime(today.Year, today.Month, 1);
                    if (future || monthLogs.Count == 0 || req == 0)
                    {
                        percentLabel = "–";
                        bg = Color.FromArgb("#E2E8F0");
                    }
                    else
                    {
                        var pct = (double)done / req;
                        percentLabel = $"{(int)Math.Round(pct * 100)}%";
                        if (pct > 0.75) bg = Color.FromArgb("#86EFAC");
                        else if (pct >= 0.5) bg = Color.FromArgb("#FDE68A");
                        else bg = Color.FromArgb("#FCA5A5");

                        if (pct > bestPct) { bestPct = pct; bestMonth = m; }
                        if (pct < worstPct) { worstPct = pct; worstMonth = m; }
                    }

                    newTiles.Add(new MonthTile
                    {
                        Month = m,
                        Year = Year,
                        Name = DateHelper.FormatCzechMonthName(m),
                        PercentLabel = percentLabel,
                        Background = bg
                    });
                }

                Tiles = newTiles;

                var ytPct = yearTotalReq == 0 ? 0 : (double)yearTotalDone / yearTotalReq;
                YearTotalProgress = ytPct;
                YearTotalLabel = $"Celková roèní úspìšnost: {(int)Math.Round(ytPct * 100)}%";
                TotalBlocksLabel = $"Splnìno blokù: {yearTotalDone} / {yearTotalReq}";
                BestMonthLabel = bestMonth == 0 ? "Nejlepší mìsíc: –" :
                    $"Nejlepší mìsíc: {DateHelper.FormatCzechMonthName(bestMonth)} ({(int)Math.Round(bestPct * 100)}%)";
                WorstMonthLabel = worstMonth == 0 ? "Nejslabší mìsíc: –" :
                    $"Nejslabší mìsíc: {DateHelper.FormatCzechMonthName(worstMonth)} ({(int)Math.Round(worstPct * 100)}%)";

                var daysWithData = yearLogs.Count(l => !l.IsSpecialDay && completions.Any(c => c.DayLogId == l.Id));
                DaysWithDataLabel = $"Dní s daty: {daysWithData} / {(DateTime.IsLeapYear(Year) ? 366 : 365)}";

                SpecialDaysLabel = $"Speciálních dní: {specialDaysCount}";

                // Count Tabor weekends in year
                int taborWeekends = 0;
                for (var d = new DateTime(Year, 1, 1); d.Year == Year; d = d.AddDays(1))
                {
                    if (TaborHelper.IsTaborFriday(d)) taborWeekends++;
                }
                TaborWeekendsLabel = $"Tábor víkendù: {taborWeekends}";

                // Longest streak
                int longest = 0, current = 0;
                foreach (var log in yearLogs.OrderBy(l => l.Date))
                {
                    if (log.IsSpecialDay) { current = 0; continue; }
                    var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                    var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired).ToList();
                    var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                    var dn = required.Count(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                    var p = required.Count == 0 ? 0 : (double)dn / required.Count;
                    if (p > 0.75) { current++; if (current > longest) longest = current; }
                    else current = 0;
                }
                LongestStreakLabel = $"Nejdelší série: {longest} dní";

                // Per required activity annual totals
                var stats = new ObservableCollection<ActivityStat>();
                var requiredCategories = new[] { Category.DotNet, Category.Project, Category.Reading, Category.Running };
                foreach (var cat in requiredCategories)
                {
                    int catDone = 0, catTotal = 0;
                    foreach (var log in yearLogs.Where(l => !l.IsSpecialDay))
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired && b.Category == cat).ToList();
                        if (required.Count == 0) continue;
                        catTotal++;
                        var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                        if (required.Any(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done)))
                            catDone++;
                    }
                    var catPct = catTotal == 0 ? 0 : (double)catDone / catTotal;
                    stats.Add(new ActivityStat
                    {
                        Name = WeekViewModel.GetCategoryCzech(cat),
                        Label = $"{catDone} / {catTotal} dní  {(int)Math.Round(catPct * 100)}%",
                        Color = BlockViewModel.GetCategoryColor(cat),
                        Progress = catPct
                    });
                }
                ActivityStats = stats;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"YearLoad error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PrevYearAsync()
        {
            Year--;
            await LoadAsync();
        }

        [RelayCommand]
        private async Task NextYearAsync()
        {
            Year++;
            await LoadAsync();
        }
    }
}
