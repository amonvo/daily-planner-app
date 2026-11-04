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
    public partial class TodayViewModel : BaseViewModel, IDisposable
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
        public partial string TodayDate { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DayTypeLabel { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string DayTypeBadgeColor { get; set; } = "#7C3AED";

        [ObservableProperty]
        public partial ObservableCollection<BlockViewModel> Blocks { get; set; } = new();

        [ObservableProperty]
        public partial int DoneCount { get; set; }

        [ObservableProperty]
        public partial int TotalRequired { get; set; }

        [ObservableProperty]
        public partial double ProgressPercent { get; set; }

        [ObservableProperty]
        public partial string ProgressLabel { get; set; } = "0 / 0 splneno (0 %)";

        [ObservableProperty]
        public partial string CurrentBlockName { get; set; } = "Zadna aktivita";

        [ObservableProperty]
        public partial string CurrentBlockTime { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CurrentBlockColor { get; set; } = "#7C3AED";

        [ObservableProperty]
        public partial string NextBlockName { get; set; } = "Zadna dalsi aktivita dnes";

        [ObservableProperty]
        public partial string NextBlockInfo { get; set; } = "Zadna dalsi aktivita dnes";

        [ObservableProperty]
        public partial string Notes { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSpecialDay { get; set; }

        [ObservableProperty]
        public partial string SpecialDayLabel { get; set; } = string.Empty;

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
                StopTimer();

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

                Blocks.Clear();
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
                StartTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadToday error: {ex}");
                ErrorMessage = "Data se nepodařilo načíst. Zkuste to znovu.";
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
            var isToday = _selectedDate.Date == DateTime.Today;
            var now = TimeOnly.FromDateTime(DateTime.Now);

            foreach (var b in Blocks)
            {
                var current = isToday && IsTimeInBlock(now, b.Block.TimeFrom, b.Block.TimeTo);
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

            var nowTs = now.ToTimeSpan();
            var next = Blocks
                .Where(b => !b.IsCurrentBlock && b.Block.TimeFrom > nowTs)
                .OrderBy(b => b.Block.TimeFrom)
                .FirstOrDefault();
            if (next is not null && isToday)
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
            if (_selectedDate.Date != DateTime.Today) return;
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

        private async void OnTimerTick(object? sender, EventArgs e)
        {
            if (SelectedDate.Date != DateTime.Today && _currentDayLog?.Date.Date == SelectedDate.Date)
            {
                SelectedDate = DateTime.Today;
                await LoadTodayAsync();
                await _notif.RescheduleForTodayAsync();
                return;
            }
            RefreshCurrentBlock();
        }

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
            try
            {
            if (block is null || _currentDayLog is null) return;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Done,
                CompletedAt = DateTime.Now
            });
            block.Status = CompletionStatus.Done;
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        
            }
            catch (Exception ex) { ErrorMessage = "Změnu se nepodařilo uložit. Zkuste to znovu."; System.Diagnostics.Debug.WriteLine(ex); }
        }

        [RelayCommand]
        private async Task MarkSkippedAsync(BlockViewModel? block)
        {
            try
            {
            if (block is null || _currentDayLog is null) return;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.Skipped,
                CompletedAt = DateTime.Now
            });
            block.Status = CompletionStatus.Skipped;
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        
            }
            catch (Exception ex) { ErrorMessage = "Změnu se nepodařilo uložit. Zkuste to znovu."; System.Diagnostics.Debug.WriteLine(ex); }
        }

        [RelayCommand]
        private async Task ResetBlockAsync(BlockViewModel? block)
        {
            try
            {
            if (block is null || _currentDayLog is null) return;
            await _db.UpsertCompletionAsync(new BlockCompletion
            {
                DayLogId = _currentDayLog.Id,
                ScheduleBlockId = block.ScheduleBlockId,
                Status = CompletionStatus.NotDone,
                CompletedAt = null
            });
            block.Status = CompletionStatus.NotDone;
            UpdateStats();
            WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
        
            }
            catch (Exception ex) { ErrorMessage = "Změnu se nepodařilo uložit. Zkuste to znovu."; System.Diagnostics.Debug.WriteLine(ex); }
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

            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) return;

            var options = new[] { "Festival", "Nemoc", "Dovolena", "Jine" };
            var result = await page.DisplayActionSheetAsync("Oznacit jako specialni den", "Zrusit", null, options);
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
            if (_currentDayLog is null) return;
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
            if (_suppressNotesSave || _currentDayLog is null) return;
            _currentDayLog.Notes = value;
            _ = SaveNotesAsync(_currentDayLog);
        }

        private async Task SaveNotesAsync(DayLog log)
        {
            try { await _db.UpdateDayLogAsync(log); }
            catch (Exception ex) { ErrorMessage = "Poznámku se nepodařilo uložit. Zkuste to znovu."; System.Diagnostics.Debug.WriteLine(ex); }
        }

        public void Dispose()
        {
            StopTimer();
        }
    }
}
