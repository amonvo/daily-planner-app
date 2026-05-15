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
    public partial class DayDetailViewModel : BaseViewModel, IDisposable
    {
        private readonly DatabaseService _db;
        private DayLog? _currentDayLog;
        private DateTime _selectedDate = DateTime.Today;
        private IDispatcherTimer? _timer;
        private bool _suppressNotesSave;

        public DayDetailViewModel(DatabaseService db)
        {
            _db = db;
            Title = "Den";
        }

        [ObservableProperty]
        private string dateLabel = string.Empty;

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
        private string progressLabel = "0 / 0 splneno (0 %)";

        [ObservableProperty]
        private string currentBlockName = "Zadna aktivita";

        [ObservableProperty]
        private string currentBlockTime = string.Empty;

        [ObservableProperty]
        private string currentBlockColor = "#7C3AED";

        [ObservableProperty]
        private string nextBlockName = "Zadna dalsi aktivita dnes";

        [ObservableProperty]
        private string nextBlockInfo = "Zadna dalsi aktivita dnes";

        [ObservableProperty]
        private string notes = string.Empty;

        [ObservableProperty]
        private bool isSpecialDay;

        [ObservableProperty]
        private string specialDayLabel = string.Empty;

        [ObservableProperty]
        private bool isPastDay;

        [ObservableProperty]
        private bool isFutureDay;

        [ObservableProperty]
        private bool isToday;

        [ObservableProperty]
        private bool canEdit = true;

        public async Task LoadDayAsync(DateTime date)
        {
            _selectedDate = date.Date;
            var today = DateTime.Today;
            IsToday = _selectedDate == today;
            IsPastDay = _selectedDate < today;
            IsFutureDay = _selectedDate > today;
            CanEdit = !IsFutureDay;

            if (IsBusy) return;
            try
            {
                IsBusy = true;
                StopTimer();

                _currentDayLog = await _db.GetOrCreateDayLogAsync(_selectedDate);
                DateLabel = DateHelper.FormatLongCzechDate(_selectedDate);
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

                var newBlocks = new ObservableCollection<BlockViewModel>();
                foreach (var sb in scheduleBlocks)
                {
                    var completion = completions.FirstOrDefault(c => c.ScheduleBlockId == sb.Id);
                    var status = completion?.Status ?? CompletionStatus.NotDone;
                    newBlocks.Add(new BlockViewModel(sb, status, false));
                }
                Blocks = newBlocks;

                _suppressNotesSave = true;
                Notes = _currentDayLog.Notes ?? string.Empty;
                _suppressNotesSave = false;

                UpdateStats();
                RefreshCurrentBlock();
                if (IsToday) StartTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDay error: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static bool IsTimeInBlock(TimeOnly now, TimeSpan from, TimeSpan to)
        {
            var nowTs = now.ToTimeSpan();
            if (from <= to)
                return nowTs >= from && nowTs < to;
            return nowTs >= from || nowTs < to;
        }

        private void UpdateStats()
        {
            if (IsSpecialDay)
            {
                TotalRequired = 0;
                DoneCount = 0;
                ProgressPercent = 0;
                ProgressLabel = "Specialni den - nezapocitava se.";
                return;
            }
            TotalRequired = Blocks.Count(b => b.IsRequired);
            DoneCount = Blocks.Count(b => b.IsRequired && b.IsCompleted);

            if (TotalRequired == 0)
            {
                ProgressPercent = 0;
                ProgressLabel = "0 / 0 splneno (0 %)";
                return;
            }

            ProgressPercent = (double)DoneCount / TotalRequired;
            if (DoneCount == TotalRequired) ProgressPercent = 1.0;
            var percent = (int)Math.Round(ProgressPercent * 100);
            ProgressLabel = $"{DoneCount} / {TotalRequired} splneno ({percent} %)";
        }

        public void RefreshCurrentBlock()
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);

            foreach (var b in Blocks)
            {
                var current = IsToday && IsTimeInBlock(now, b.Block.TimeFrom, b.Block.TimeTo);
                if (b.IsCurrentBlock != current)
                    b.IsCurrentBlock = current;
            }

            var current2 = Blocks.FirstOrDefault(b => b.IsCurrentBlock);
            if (current2 is not null)
            {
                CurrentBlockName = current2.ActivityName;
                CurrentBlockTime = current2.TimeLabel;
                CurrentBlockColor = current2.CategoryColor;
            }
            else
            {
                CurrentBlockName = "Zadna aktivita";
                CurrentBlockTime = string.Empty;
                CurrentBlockColor = "#64748B";
            }

            if (!IsToday)
            {
                NextBlockName = string.Empty;
                NextBlockInfo = string.Empty;
                return;
            }

            var nowTs = now.ToTimeSpan();
            var next = Blocks
                .Where(b => !b.IsCurrentBlock && b.Block.TimeFrom > nowTs)
                .OrderBy(b => b.Block.TimeFrom)
                .FirstOrDefault();
            if (next is not null)
            {
                NextBlockName = $"Dalsi: {next.ActivityName} v {next.Block.TimeFrom:hh\\:mm}";
                NextBlockInfo = $"Dalsi: {next.ActivityName} v {next.Block.TimeFrom:hh\\:mm}";
            }
            else
            {
                NextBlockName = "Zadna dalsi aktivita dnes";
                NextBlockInfo = "Zadna dalsi aktivita dnes";
            }
        }

        private void StartTimer()
        {
            try
            {
                _timer = Application.Current?.Dispatcher.CreateTimer();
                if (_timer is null) return;
                _timer.Interval = TimeSpan.FromSeconds(30);
                _timer.Tick += OnTimerTick;
                _timer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timer error: {ex}");
            }
        }

        private void OnTimerTick(object? sender, EventArgs e) => RefreshCurrentBlock();

        public void StopTimer()
        {
            try
            {
                if (_timer is not null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTimerTick;
                    _timer = null;
                }
            }
            catch { }
        }

        [RelayCommand]
        private async Task MarkDoneAsync(BlockViewModel? block)
        {
            if (!CanEdit || block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.Done;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Done,
                CompletedAt = DateTime.Now
            });
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        }

        [RelayCommand]
        private async Task MarkSkippedAsync(BlockViewModel? block)
        {
            if (!CanEdit || block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.Skipped;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Skipped,
                CompletedAt = DateTime.Now
            });
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        }

        [RelayCommand]
        private async Task ResetBlockAsync(BlockViewModel? block)
        {
            if (!CanEdit || block is null || _currentDayLog is null) return;
            block.Status = CompletionStatus.NotDone;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.NotDone,
                CompletedAt = null
            });
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        }

        [RelayCommand]
        private async Task ToggleSpecialDayAsync()
        {
            if (!CanEdit || _currentDayLog is null) return;
            if (IsSpecialDay)
            {
                await ClearSpecialDayAsync();
                return;
            }

            var page = Application.Current?.MainPage;
            if (page is null) return;

            var options = new[] { "Festival", "Nemoc", "Dovolena", "Jine" };
            var result = await page.DisplayActionSheet("Oznacit jako specialni den", "Zrusit", null, options);
            if (result is null || result == "Zrusit") return;

            string label = result;
            if (result == "Jine")
            {
                var custom = await page.DisplayPromptAsync("Specialni den", "Zadej nazev:", "OK", "Zrusit", "Napr. Vylet");
                if (string.IsNullOrWhiteSpace(custom)) return;
                label = custom.Trim();
            }

            _currentDayLog.IsSpecialDay = true;
            _currentDayLog.SpecialDayLabel = label;
            await _db.UpdateDayLogAsync(_currentDayLog);
            IsSpecialDay = true;
            SpecialDayLabel = label;
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        }

        [RelayCommand]
        private async Task ClearSpecialDayAsync()
        {
            if (!CanEdit || _currentDayLog is null) return;
            _currentDayLog.IsSpecialDay = false;
            _currentDayLog.SpecialDayLabel = null;
            await _db.UpdateDayLogAsync(_currentDayLog);
            IsSpecialDay = false;
            SpecialDayLabel = string.Empty;
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        }

        partial void OnNotesChanged(string value)
        {
            if (_suppressNotesSave || _currentDayLog is null || !CanEdit) return;
            _currentDayLog.Notes = value;
            _ = _db.UpdateDayLogAsync(_currentDayLog);
        }

        public void Dispose()
        {
            StopTimer();
        }
    }
}
