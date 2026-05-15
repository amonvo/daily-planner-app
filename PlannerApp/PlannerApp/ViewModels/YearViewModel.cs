using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using PlannerApp.Helpers;
using PlannerApp.Messages;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.ViewModels
{
    public partial class MonthTile : ObservableObject
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PercentLabel { get; set; } = "-";
        public Color Background { get; set; } = Color.FromArgb("#E2E8F0");
    }

    public partial class ActivityStat : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#7C3AED";
        public double Progress { get; set; }
    }

    public partial class YearViewModel : BaseViewModel, IDisposable
    {
        private readonly DatabaseService _db;

        public YearViewModel(DatabaseService db)
        {
            _db = db;
            Title = "Rok";
            year = DateTime.Today.Year;
            if (year < 2026) year = 2026;

            WeakReferenceMessenger.Default.Register<CompletionChangedMessage>(this, async (r, m) => await LoadAsync());
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

                Tiles.Clear();
                ActivityStats.Clear();

                var yearLogs = await _db.GetYearLogsAsync(Year);
                var completions = await _db.GetCompletionsForDaysAsync(yearLogs.Select(l => l.Id).ToList());

                // Cache per-date schedule blocks (handles Tabor)
                var perDateBlocks = new Dictionary<DateTime, List<ScheduleBlock>>();
                foreach (var log in yearLogs)
                {
                    perDateBlocks[log.Date.Date] = await _db.GetScheduleBlocksForDateAsync(log.Date);
                }

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
                        if (!perDateBlocks.TryGetValue(log.Date.Date, out var dayBlocks)) continue;
                        var required = dayBlocks.Where(b => b.IsRequired).ToList();
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
                        percentLabel = "-";
                        bg = Color.FromArgb("#E2E8F0");
                    }
                    else
                    {
                        var pct = (double)done / req;
                        if (done == req) pct = 1.0;
                        percentLabel = $"{(int)Math.Round(pct * 100)} %";
                        if (pct >= 0.75) bg = Color.FromArgb("#86EFAC");
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
                if (yearTotalReq > 0 && yearTotalDone == yearTotalReq) ytPct = 1.0;
                YearTotalProgress = ytPct;
                YearTotalLabel = $"Celkova rocni uspesnost: {(int)Math.Round(ytPct * 100)} %";
                TotalBlocksLabel = $"Splneno bloku: {yearTotalDone} / {yearTotalReq}";
                BestMonthLabel = bestMonth == 0 ? "Nejlepsi mesic: -" :
                    $"Nejlepsi mesic: {DateHelper.FormatCzechMonthName(bestMonth)} ({(int)Math.Round(bestPct * 100)} %)";
                WorstMonthLabel = worstMonth == 0 ? "Nejslabsi mesic: -" :
                    $"Nejslabsi mesic: {DateHelper.FormatCzechMonthName(worstMonth)} ({(int)Math.Round(worstPct * 100)} %)";

                var daysWithData = yearLogs.Count(l => !l.IsSpecialDay && completions.Any(c => c.DayLogId == l.Id));
                DaysWithDataLabel = $"Dni s daty: {daysWithData} / {(DateTime.IsLeapYear(Year) ? 366 : 365)}";
                SpecialDaysLabel = $"Specialnich dni: {specialDaysCount}";

                int taborWeekends = 0;
                for (var d = new DateTime(Year, 1, 1); d.Year == Year; d = d.AddDays(1))
                {
                    if (TaborHelper.IsTaborFriday(d)) taborWeekends++;
                }
                TaborWeekendsLabel = $"Tabor vikendu: {taborWeekends}";

                int longest = 0, current = 0;
                foreach (var log in yearLogs.OrderBy(l => l.Date))
                {
                    if (log.IsSpecialDay) { current = 0; continue; }
                    if (!perDateBlocks.TryGetValue(log.Date.Date, out var dayBlocks)) { current = 0; continue; }
                    var required = dayBlocks.Where(b => b.IsRequired).ToList();
                    var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                    var dn = required.Count(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));
                    var p = required.Count == 0 ? 0 : (double)dn / required.Count;
                    if (p >= 0.75) { current++; if (current > longest) longest = current; }
                    else current = 0;
                }
                LongestStreakLabel = $"Nejdelsi serie: {longest} dni";

                var stats = new ObservableCollection<ActivityStat>();
                var requiredCategories = new[] { Category.DotNet, Category.Project, Category.Reading, Category.Running };
                foreach (var cat in requiredCategories)
                {
                    int catDone = 0, catTotal = 0;
                    foreach (var log in yearLogs.Where(l => !l.IsSpecialDay))
                    {
                        if (!perDateBlocks.TryGetValue(log.Date.Date, out var dayBlocks)) continue;
                        var required = dayBlocks.Where(b => b.IsRequired && b.Category == cat).ToList();
                        if (required.Count == 0) continue;
                        catTotal++;
                        var dc = completions.Where(c => c.DayLogId == log.Id).ToList();
                        if (required.Any(b => dc.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done)))
                            catDone++;
                    }
                    var catPct = catTotal == 0 ? 0 : (double)catDone / catTotal;
                    if (catTotal > 0 && catDone == catTotal) catPct = 1.0;
                    stats.Add(new ActivityStat
                    {
                        Name = WeekViewModel.GetCategoryCzech(cat),
                        Label = $"{catDone} / {catTotal} dni  {(int)Math.Round(catPct * 100)} %",
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

        public Task LoadDataAsync() => LoadAsync();

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

        public void Dispose()
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        ~YearViewModel()
        {
            try { WeakReferenceMessenger.Default.UnregisterAll(this); } catch { }
        }
    }
}
