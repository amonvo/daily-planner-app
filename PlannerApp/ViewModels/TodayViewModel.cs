using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlannerApp.Helpers;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.ViewModels
{
    public partial class TodayViewModel : BaseViewModel
    {
        private readonly DatabaseService _db;
        private readonly NotificationService _notif;
        private DayLog? _currentDayLog;
        private DateTime _selectedDate = DateTime.Today;
        private IDispatcherTimer? _timer;
        private bool _suppressNotesSave;

        public TodayViewModel(DatabaseService db, NotificationService notif)
        {
            _db = db;
            _notif = notif;
            Title = "Dnes";
        }

        [ObservableProperty]
        private string todayDate = string.Empty;

        [ObservableProperty]
        private string dayTypeLabel = string.Empty;

        [ObservableProperty]
        private string dayTypeBadgeColor = "#7C3AED";

        [ObservableProperty]
        private ObservableCollection<BlockViewModel> blocks = new();

        [ObservableProperty]
        private int doneCount;

        [ObservableProperty]
        private int totalRequired;

        [ObservableProperty]
        private double progressPercent;

        [ObservableProperty]
        private string progressLabel = "0 / 0 splnìno (0%)";

        [ObservableProperty]
        private string currentBlockName = "–";

        [ObservableProperty]
        private string currentBlockTime = string.Empty;

        [ObservableProperty]
        private string currentBlockColor = "#7C3AED";

        [ObservableProperty]
        private string nextBlockName = "–";

        [ObservableProperty]
        private string nextBlockInfo = string.Empty;

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private bool isSpecialDay;

        [ObservableProperty]
        private string specialDayLabel = string.Empty;

        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged();
                }
            }
        }

        [RelayCommand]
        public async Task LoadTodayAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                _currentDayLog = await _db.GetOrCreateDayLogAsync(_selectedDate);
                TodayDate = DateHelper.FormatLongCzechDate(_selectedDate);
                var dayType = _currentDayLog.DayType;
                DayTypeLabel = DateHelper.GetDayTypeLabel(dayType);
                DayTypeBadgeColor = dayType switch
                {
                    DayType.Workday => "#1E40AF",
                    DayType.Weekend => "#059669",
                    DayType.Holiday => "#D97706",
                    DayType.Tabor => "#0891B2",
                    _ => "#7C3AED"
                };

                IsSpecialDay = _currentDayLog.IsSpecialDay;
                SpecialDayLabel = _currentDayLog.SpecialDayLabel ?? string.Empty;

                var scheduleBlocks = await _db.GetScheduleBlocksForDateAsync(_selectedDate);
                var completions = await _db.GetCompletionsForDayAsync(_currentDayLog.Id);
                var now = DateTime.Now.TimeOfDay;
                var isToday = _selectedDate.Date == DateTime.Today;

                var newBlocks = new ObservableCollection<BlockViewModel>();
                foreach (var sb in scheduleBlocks)
                {
                    var completion = completions.FirstOrDefault(c => c.ScheduleBlockId == sb.Id);
                    var status = completion?.Status ?? CompletionStatus.NotDone;
                    var isCurrent = isToday && IsTimeInBlock(now, sb.TimeFrom, sb.TimeTo);
                    newBlocks.Add(new BlockViewModel(sb, status, isCurrent));
                }
                Blocks = newBlocks;

                _suppressNotesSave = true;
                Notes = _currentDayLog.Notes ?? string.Empty;
                _suppressNotesSave = false;

                UpdateStats();
                UpdateCurrentNext();
                StartTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadToday error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static bool IsTimeInBlock(TimeSpan now, TimeSpan from, TimeSpan to)
        {
            if (from <= to)
                return now >= from && now < to;
            return now >= from || now < to;
        }

        private void UpdateStats()
        {
            if (IsSpecialDay)
            {
                TotalRequired = 0;
                DoneCount = 0;
                ProgressPercent = 0;
                ProgressLabel = "Speciální den – nezapoèítává se.";
                return;
            }
            TotalRequired = Blocks.Count(b => b.IsRequired);
            DoneCount = Blocks.Count(b => b.IsRequired && b.IsCompleted);
            ProgressPercent = TotalRequired == 0 ? 0 : (double)DoneCount / TotalRequired;
            var percent = (int)Math.Round(ProgressPercent * 100);
            ProgressLabel = $"{DoneCount} / {TotalRequired} splnìno ({percent}%)";
        }

        private void UpdateCurrentNext()
        {
            var current = Blocks.FirstOrDefault(b => b.IsCurrentBlock);
            if (current is not null)
            {
                CurrentBlockName = current.ActivityName;
                CurrentBlockTime = current.TimeLabel;
                CurrentBlockColor = current.CategoryColor;
            }
            else
            {
                CurrentBlockName = "–";
                CurrentBlockTime = string.Empty;
                CurrentBlockColor = "#64748B";
            }

            var now = DateTime.Now.TimeOfDay;
            var next = Blocks
                .Where(b => b.Block.TimeFrom > now)
                .OrderBy(b => b.Block.TimeFrom)
                .FirstOrDefault();
            if (next is not null)
            {
                NextBlockName = next.ActivityName;
                NextBlockInfo = $"Další: {next.ActivityName} v {next.Block.TimeFrom:hh\\:mm}";
            }
            else
            {
                NextBlockName = "–";
                NextBlockInfo = "Žádná další aktivita dnes.";
            }
        }

        private void StartTimer()
        {
            if (_timer is not null) return;
            try
            {
                _timer = Application.Current?.Dispatcher.CreateTimer();
                if (_timer is null) return;
                _timer.Interval = TimeSpan.FromSeconds(60);
                _timer.Tick += (_, _) => RefreshCurrent();
                _timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer error: {ex}");
            }
        }

        public void StopTimer()
        {
            try
            {
                _timer?.Stop();
                _timer = null;
            }
            catch { }
        }

        private void RefreshCurrent()
        {
            var now = DateTime.Now.TimeOfDay;
            var isToday = _selectedDate.Date == DateTime.Today;
            foreach (var b in Blocks)
            {
                var current = isToday && IsTimeInBlock(now, b.Block.TimeFrom, b.Block.TimeTo);
                if (b.IsCurrentBlock != current)
                    b.IsCurrentBlock = current;
            }
            UpdateCurrentNext();
        }

        [RelayCommand]
        private async Task MarkDoneAsync(BlockViewModel? block)
        {
            if (block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.Done;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Done,
                CompletedAt = DateTime.Now
            });
            UpdateStats();
        }

        [RelayCommand]
        private async Task MarkSkippedAsync(BlockViewModel? block)
        {
            if (block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.Skipped;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Skipped,
                CompletedAt = DateTime.Now
            });
            UpdateStats();
        }

        [RelayCommand]
        private async Task ResetBlockAsync(BlockViewModel? block)
        {
            if (block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.NotDone;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.NotDone,
                CompletedAt = null
            });
            UpdateStats();
        }

        [RelayCommand]
        private async Task ToggleSpecialDayAsync()
        {
            if (_currentDayLog is null) return;
            if (IsSpecialDay)
            {
                await ClearSpecialDayAsync();
                return;
            }

            var page = Application.Current?.MainPage;
            if (page is null) return;

            var options = new[] { "Festival", "Nemoc", "Dovolená", "Jiné…" };
            var result = await page.DisplayActionSheet("Oznaèit jako speciální den", "Zrušit", null, options);
            if (result is null || result == "Zrušit") return;

            string label = result;
            if (result == "Jiné…")
            {
                var custom = await page.DisplayPromptAsync("Speciální den", "Zadej název:", "OK", "Zrušit", "Napø. Výlet");
                if (string.IsNullOrWhiteSpace(custom)) return;
                label = custom.Trim();
            }

            _currentDayLog.IsSpecialDay = true;
            _currentDayLog.SpecialDayLabel = label;
            await _db.UpdateDayLogAsync(_currentDayLog);
            IsSpecialDay = true;
            SpecialDayLabel = label;
            UpdateStats();
        }

        [RelayCommand]
        private async Task ClearSpecialDayAsync()
        {
            if (_currentDayLog is null) return;
            _currentDayLog.IsSpecialDay = false;
            _currentDayLog.SpecialDayLabel = null;
            await _db.UpdateDayLogAsync(_currentDayLog);
            IsSpecialDay = false;
            SpecialDayLabel = string.Empty;
            UpdateStats();
        }

        partial void OnNotesChanged(string value)
        {
            if (_suppressNotesSave || _currentDayLog is null) return;
            _currentDayLog.Notes = value;
            _ = _db.UpdateDayLogAsync(_currentDayLog);
        }
    }
}
