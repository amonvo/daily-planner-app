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
        public string PercentLabel { get; set; } = "—";
        public Color Background { get; set; } = Color.FromArgb("#E2E8F0");
    }

    public partial class ActivityStat : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#7C3AED";
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
                int bestMonth = 0; double bestPct = -1;

                for (int m = 1; m <= 12; m++)
                {
                    var monthLogs = yearLogs.Where(l => l.Date.Month == m).ToList();
                    int done = 0, req = 0;
                    foreach (var log in monthLogs)
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired).ToList();
                        var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                        done += required.Count(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                        req += required.Count;
                    }

                    string percentLabel;
                    Color bg;
                    var future = new DateTime(Year, m, 1) > new DateTime(today.Year, today.Month, 1);
                    if (future || monthLogs.Count == 0 || req == 0)
                    {
                        percentLabel = "—";
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

                DaysWithDataLabel = $"Dnù s daty: {yearLogs.Count(l => completions.Any(c => c.DayLogId == l.Id))} / {(DateTime.IsLeapYear(Year) ? 366 : 365)}";
                BestMonthLabel = bestMonth == 0 ? "Nejlepší mìsíc: —" :
                    $"Nejlepší mìsíc: {DateHelper.FormatCzechMonthName(bestMonth)} ({(int)Math.Round(bestPct * 100)}%)";

                // Longest streak across year (>75%)
                int longest = 0, current = 0;
                foreach (var log in yearLogs.OrderBy(l => l.Date))
                {
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
                var requiredCategories = allBlocks.Where(b => b.IsRequired).Select(b => b.Category).Distinct();
                foreach (var cat in requiredCategories)
                {
                    int done = 0;
                    foreach (var log in yearLogs)
                    {
                        var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                        var required = allBlocks.Where(b => b.DayType == scheduleDay && b.IsRequired && b.Category == cat).ToList();
                        if (required.Count == 0) continue;
                        var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                        if (required.Any(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done)))
                            done++;
                    }
                    stats.Add(new ActivityStat
                    {
                        Name = WeekViewModel.GetCategoryCzech(cat),
                        Label = $"{done} dní",
                        Color = BlockViewModel.GetCategoryColor(cat)
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
