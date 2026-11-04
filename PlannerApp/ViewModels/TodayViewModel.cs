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
        public partial string ProgressLabel { get; set; } = "0 / 0 splněno (0 %)";

        [ObservableProperty]
        public partial string CurrentBlockName { get; set; } = "Zadna aktivita";

        [ObservableProperty]
        public partial string CurrentBlockTime { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CurrentBlockColor { get; set; } = "#7C3AED";

        [ObservableProperty]
        public partial string NextBlockName { get; set; } = "Pro dnešek je váš plán kompletní.";

        [ObservableProperty]
        public partial string NextBlockInfo { get; set; } = "Pro dnešek je váš plán kompletní.";

        [ObservableProperty]
        public partial string Notes { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsSpecialDay { get; set; }

        [ObservableProperty]
        public partial string SpecialDayLabel { get; set; } = string.Empty;


        [ObservableProperty] public partial List<TrendPoint> RecentTrend { get; set; } = [];
        [ObservableProperty] public partial string ActionMessage { get; set; } = string.Empty;
        [ObservableProperty] public partial bool CanUndo { get; set; }
        [ObservableProperty] public partial string RemainingTime { get; set; } = string.Empty;
        private string filter = "all";
        private BlockViewModel? undoBlock;
        private CompletionStatus undoStatus;
        private readonly SemaphoreSlim actionLock = new(1, 1);
        public IEnumerable<BlockViewModel> VisibleBlocks => filter switch {
            "pending" => Blocks.Where(block => block.IsRequired && block.IsNotDone),
            "focus" => Blocks.Where(block => block.IsRequired),
            _ => Blocks
        };
        public string Greeting => DateTime.Now.Hour < 11 ? "Dobré ráno." : DateTime.Now.Hour < 18 ? "Hezký den." : "Klidný večer.";
        public string RemainingLabel => IsSpecialDay ? "Volnější den" : $"{Math.Max(0, TotalRequired - DoneCount)} zbývá";
        public string DoneLabel => $"{DoneCount} hotovo";
        public string ProgressHint => IsSpecialDay ? "Dnes si dejte prostor. Tento den se do statistik nezapočítává." : TotalRequired == 0 ? "Dnes nemáte povinné aktivity. Užijte si volnější tempo." : DoneCount == TotalRequired ? "Pro dnešek máte splněno. Dopřejte si zasloužený odpočinek." : "Jeden krok po druhém. I malý pokrok se počítá.";
        public bool HasCurrentAction => Blocks.Any(block => block.IsCurrentBlock && block.IsNotDone);
        public bool HasVisibleBlocks => VisibleBlocks.Any();
        private static Color FilterColor(bool active) => active ? Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#365C48" : "#E2EBD9") : Colors.Transparent;
        public Color AllFilterBackground => FilterColor(filter == "all");
        public Color PendingFilterBackground => FilterColor(filter == "pending");
        public Color FocusFilterBackground => FilterColor(filter == "focus");
        [RelayCommand] private void SetFilter(string value)
        {
            filter = value;
            OnPropertyChanged(nameof(VisibleBlocks)); OnPropertyChanged(nameof(HasVisibleBlocks));
            OnPropertyChanged(nameof(AllFilterBackground)); OnPropertyChanged(nameof(PendingFilterBackground)); OnPropertyChanged(nameof(FocusFilterBackground));
        }
        [RelayCommand] private async Task CompleteCurrentAsync() => await SaveStatusAsync(Blocks.FirstOrDefault(block => block.IsCurrentBlock), CompletionStatus.Done);
        [RelayCommand] private async Task OpenTrendDayAsync(TrendPoint point) => await Shell.Current.GoToAsync($"daydetail?date={point.Date:yyyy-MM-dd}");
        [RelayCommand] private async Task OpenWeekAsync() => await Shell.Current.GoToAsync("//week");
        [RelayCommand] private async Task UndoLastAsync()
        {
            if (undoBlock is null || !CanUndo) return;
            await SaveStatusAsync(undoBlock, undoStatus, false);
        }

        private async Task SaveStatusAsync(BlockViewModel? block, CompletionStatus status, bool allowUndo = true)
        {
            if (block is null || _currentDayLog is null) return;
            var dayLog = _currentDayLog;
            await actionLock.WaitAsync();
            try {
                ErrorMessage = string.Empty;
                var previous = block.Status;
                await _db.UpsertCompletionAsync(new BlockCompletion { DayLogId = dayLog.Id, ScheduleBlockId = block.ScheduleBlockId, Status = status, CompletedAt = status == CompletionStatus.NotDone ? null : DateTime.Now });
                if (_currentDayLog?.Id != dayLog.Id) return;
                block.Status = status;
                undoBlock = block; undoStatus = previous; CanUndo = allowUndo;
                ActionMessage = allowUndo ? $"{block.ActivityName} · {(status == CompletionStatus.Done ? "splněno" : status == CompletionStatus.Skipped ? "přeskočeno" : "vráceno do plánu")}" : "Poslední změna byla vrácena.";
                UpdateStats(); RefreshCurrentBlock();
                await RefreshTrendAsync();
                WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
            }
            catch (Exception exception) { ErrorMessage = "Změnu se nepodařilo uložit. Zkuste to znovu."; System.Diagnostics.Debug.WriteLine(exception); }
            finally { actionLock.Release(); }
        }

        private async Task RefreshTrendAsync()
        {
            try { RecentTrend = await new InsightService(_db).GetDaysAsync(DateTime.Today.AddDays(-6), DateTime.Today); }
            catch (Exception exception) { ErrorMessage = "Přehled posledních dní se nepodařilo obnovit. Zkuste stránku otevřít znovu."; System.Diagnostics.Debug.WriteLine(exception); }
        }

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
                ErrorMessage = string.Empty;
                CanUndo = false;
                ActionMessage = string.Empty;

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
                await RefreshTrendAsync();
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
            TotalRequired = IsSpecialDay ? 0 : Blocks.Count(block => block.IsRequired);
            DoneCount = IsSpecialDay ? 0 : Blocks.Count(block => block.IsRequired && block.IsCompleted);
            ProgressPercent = TotalRequired == 0 ? 0 : (double)DoneCount / TotalRequired;
            ProgressLabel = $"{DoneCount} z {TotalRequired} povinných aktivit";
            foreach (var name in new[] { nameof(RemainingLabel), nameof(DoneLabel), nameof(ProgressHint), nameof(VisibleBlocks), nameof(HasVisibleBlocks) }) OnPropertyChanged(name);
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
                var end = _selectedDate.Date.Add(current2.Block.TimeTo);
                if (current2.Block.TimeTo <= current2.Block.TimeFrom && now.ToTimeSpan() >= current2.Block.TimeFrom) end = end.AddDays(1);
                RemainingTime = $"Ještě {Math.Max(1, (int)Math.Ceiling((end - DateTime.Now).TotalMinutes))} min";
            }
            else
            {
                CurrentBlockName = "Chvilka pro sebe";
                RemainingTime = "V plánu teď nemáte žádnou aktivitu.";
                CurrentBlockTime = string.Empty;
                CurrentBlockColor = "#64748B";
            }

            OnPropertyChanged(nameof(HasCurrentAction));
            var nowTs = now.ToTimeSpan();
            var next = Blocks
                .Where(b => !b.IsCurrentBlock && b.Block.TimeFrom > nowTs)
                .OrderBy(b => b.Block.TimeFrom)
                .FirstOrDefault();
            if (next is not null && isToday)
            {
                NextBlockName = $"Potom: {next.ActivityName} v {next.Block.TimeFrom:hh\\:mm}";
                NextBlockInfo = $"Potom: {next.ActivityName} v {next.Block.TimeFrom:hh\\:mm}";
            }
            else
            {
                NextBlockName = "Pro dnešek je váš plán kompletní.";
                NextBlockInfo = "Pro dnešek je váš plán kompletní.";
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
            await SaveStatusAsync(block, CompletionStatus.Done);
        }

        [RelayCommand]
        private async Task MarkSkippedAsync(BlockViewModel? block)
        {
            await SaveStatusAsync(block, CompletionStatus.Skipped);
        }

        [RelayCommand]
        private async Task ResetBlockAsync(BlockViewModel? block)
        {
            await SaveStatusAsync(block, CompletionStatus.NotDone);
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

            var options = new[] { "Festival", "Nemoc", "Dovolená", "Jiné" };
            var result = await page.DisplayActionSheetAsync("Uvolnit dnešní plán", "Zrušit", null, options);
            if (result is null || result == "Zrušit") return;

            string label = result;
            if (result == "Jiné")
            {
                var custom = await page.DisplayPromptAsync("Zvláštní den", "Zadej nazev:", "OK", "Zrušit", "Napr. Vylet");
                if (string.IsNullOrWhiteSpace(custom)) return;
                label = custom.Trim();
            }

            _currentDayLog.IsSpecialDay = true;
            _currentDayLog.SpecialDayLabel = label;
            await _db.UpdateDayLogAsync(_currentDayLog);
            IsSpecialDay = true;
            SpecialDayLabel = label;
            UpdateStats();
            await RefreshTrendAsync();
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
            await RefreshTrendAsync();
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
