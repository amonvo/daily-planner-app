using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.ViewModels
{
    public partial class WeekDaySummary : ObservableObject
    {
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public string DateLabel { get; set; } = string.Empty;
        public string DayTypeLabel { get; set; } = string.Empty;
        public string DayTypeColor { get; set; } = "#64748B";
        public int Done { get; set; }
        public int Required { get; set; }
        public double Progress { get; set; }
        public string PercentLabel { get; set; } = "0%";
    }

    public partial class CategorySummary : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public int Done { get; set; }
        public int Total { get; set; }
        public double Progress { get; set; }
        public string Bar { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Color { get; set; } = "#7C3AED";
    }

    public partial class WeekViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;

        public WeekViewModel(DatabaseService db)
        {
            _db = db;
            Title = "Týden";
            var today = DateTime.Today;
            weekNumber = DateHelper.GetIsoWeekNumber(today);
            year = DateHelper.GetIsoWeekYear(today);
        }

        [ObservableProperty]
        private int weekNumber;

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string weekLabel = string.Empty;

        [ObservableProperty]
        private string dateRangeLabel = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WeekDaySummary> days = new();

        [ObservableProperty]
        private ObservableCollection<CategorySummary> categories = new();

        [ObservableProperty]
        private string weekTotalLabel = string.Empty;

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                WeekLabel = $"Týden {WeekNumber} / {Year}";
                var start = DateHelper.GetIsoWeekStart(Year, WeekNumber);
                var end = start.AddDays(6);
                DateRangeLabel = $"{start:d. M.} – {end:d. M. yyyy}";

                var allBlocks = await _db.GetAllScheduleBlocksAsync();
                var newDays = new ObservableCollection<WeekDaySummary>();

                var weekDayLogs = new List<DayLog>();
                for (int i = 0; i < 7; i++)
                {
                    var date = start.AddDays(i);
                    var log = await _db.GetOrCreateDayLogAsync(date);
                    weekDayLogs.Add(log);
                }

                var completions = await _db.GetCompletionsForDaysAsync(weekDayLogs.Select(d => d.Id).ToList());

                int totalDone = 0, totalReq = 0;
                var perCategory = new Dictionary<Category, (int done, int total)>();

                foreach (var log in weekDayLogs)
                {
                    var scheduleDay = DateHelper.GetScheduleDayType(log.Date);
                    var blocks = allBlocks.Where(b => b.DayType == scheduleDay).ToList();
                    var required = blocks.Where(b => b.IsRequired).ToList();
                    var dayCompletions = completions.Where(c => c.DayLogId == log.Id).ToList();
                    int done = required.Count(b => dayCompletions.Any(c => c.ScheduleBlockId == b.Id && c.Status == CompletionStatus.Done));

                    foreach (var rb in required)
                    {
                        if (!perCategory.ContainsKey(rb.Category))
                            perCategory[rb.Category] = (0, 0);
                        var current = perCategory[rb.Category];
                        var isDone = dayCompletions.Any(c => c.ScheduleBlockId == rb.Id && c.Status == CompletionStatus.Done);
                        perCategory[rb.Category] = (current.done + (isDone ? 1 : 0), current.total + 1);
                    }

                    totalDone += done;
                    totalReq += required.Count;

                    var progress = required.Count == 0 ? 0 : (double)done / required.Count;
                    newDays.Add(new WeekDaySummary
                    {
                        Date = log.Date,
                        DayName = DateHelper.FormatCzechDayName(log.Date),
                        DateLabel = log.Date.ToString("d. M."),
                        DayTypeLabel = DateHelper.GetDayTypeLabel(log.DayType),
                        DayTypeColor = log.DayType switch
                        {
                            DayType.Workday => "#1E40AF",
                            DayType.Weekend => "#059669",
                            DayType.Holiday => "#D97706",
                            DayType.Tabor => "#0891B2",
                            _ => "#64748B"
                        },
                        Done = done,
                        Required = required.Count,
                        Progress = progress,
                        PercentLabel = $"{(int)Math.Round(progress * 100)}%"
                    });
                }

                Days = newDays;

                var newCats = new ObservableCollection<CategorySummary>();
                foreach (var kv in perCategory.OrderBy(k => k.Key))
                {
                    var progress = kv.Value.total == 0 ? 0 : (double)kv.Value.done / kv.Value.total;
                    var bars = (int)Math.Round(progress * 12);
                    newCats.Add(new CategorySummary
                    {
                        Name = GetCategoryCzech(kv.Key),
                        Done = kv.Value.done,
                        Total = kv.Value.total,
                        Progress = progress,
                        Bar = new string('?', bars) + new string('?', 12 - bars),
                        Label = $"{kv.Value.done}/{kv.Value.total}  {(int)Math.Round(progress * 100)}%",
                        Color = BlockViewModel.GetCategoryColor(kv.Key)
                    });
                }
                Categories = newCats;

                var weekPercent = totalReq == 0 ? 0 : (int)Math.Round((double)totalDone / totalReq * 100);
                WeekTotalLabel = $"Celkem: {totalDone} / {totalReq} ({weekPercent}%)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WeekLoad error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public static string GetCategoryCzech(Category c) => c switch
        {
            Category.Sleep => "Spánek",
            Category.Routine => "Rutina",
            Category.Work => "Práce",
            Category.Commute => "Cesta",
            Category.DotNet => ".NET studium",
            Category.Project => "Vedlejší projekt",
            Category.Reading => "Ètení",
            Category.Running => "Bìžící pás",
            Category.FreeTime => "Volný èas",
            _ => c.ToString()
        };

        [RelayCommand]
        private async Task PrevWeekAsync()
        {
            var start = DateHelper.GetIsoWeekStart(Year, WeekNumber).AddDays(-7);
            WeekNumber = DateHelper.GetIsoWeekNumber(start);
            Year = DateHelper.GetIsoWeekYear(start);
            await LoadAsync();
        }

        [RelayCommand]
        private async Task NextWeekAsync()
        {
            var start = DateHelper.GetIsoWeekStart(Year, WeekNumber).AddDays(7);
            WeekNumber = DateHelper.GetIsoWeekNumber(start);
            Year = DateHelper.GetIsoWeekYear(start);
            await LoadAsync();
        }
    }
}
