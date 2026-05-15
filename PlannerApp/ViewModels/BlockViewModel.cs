using CommunityToolkit.Mvvm.ComponentModel;
using PlannerApp.Helpers;
using PlannerApp.Models;

namespace PlannerApp.ViewModels
{
    public partial class BlockViewModel : ObservableObject
    {
        public ScheduleBlock Block { get; }

        public BlockViewModel(ScheduleBlock block, CompletionStatus status, bool isCurrent)
        {
            Block = block;
            this.status = status;
            this.isCurrentBlock = isCurrent;
        }

        [ObservableProperty]
        private CompletionStatus status;

        [ObservableProperty]
        private bool isCurrentBlock;

        public int ScheduleBlockId => Block.Id;
        public string ActivityName => Block.ActivityName;
        public string TimeLabel => DateHelper.FormatTimeRange(Block.TimeFrom, Block.TimeTo);
        public bool IsRequired => Block.IsRequired;
        public Category Category => Block.Category;

        public bool IsCompleted => Status == CompletionStatus.Done;
        public bool IsSkipped => Status == CompletionStatus.Skipped;
        public bool IsNotDone => Status == CompletionStatus.NotDone;

        public string CategoryColor => GetCategoryColor(Block.Category);

        public Color BackgroundColor
        {
            get
            {
                if (IsCompleted) return Color.FromArgb("#ECFDF5");
                if (IsSkipped) return Color.FromArgb("#F1F5F9");
                if (IsCurrentBlock) return Color.FromArgb("#FEF3C7");
                return Colors.Transparent;
            }
        }

        /// <summary>
        /// Returns dark text for light backgrounds, white for dark/saturated backgrounds.
        /// </summary>
        public Color TextColor
        {
            get
            {
                var bg = BackgroundColor;
                if (bg == Colors.Transparent) return Color.FromArgb("#1E293B");
                // Light backgrounds: completed (light green), current (light yellow), skipped (light gray)
                // All of these are light — use dark text
                return Color.FromArgb("#1E293B");
            }
        }

        public static string GetCategoryColor(Category category) => category switch
        {
            Category.DotNet => "#7C3AED",
            Category.Project => "#D97706",
            Category.Reading => "#059669",
            Category.Running => "#16A34A",
            Category.FreeTime => "#6B7280",
            Category.Work => "#1E40AF",
            Category.Sleep => "#1E293B",
            Category.Routine => "#475569",
            Category.Commute => "#0891B2",
            _ => "#64748B"
        };

        partial void OnStatusChanged(CompletionStatus value)
        {
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsSkipped));
            OnPropertyChanged(nameof(IsNotDone));
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
        }

        partial void OnIsCurrentBlockChanged(bool value)
        {
            OnPropertyChanged(nameof(BackgroundColor));
            OnPropertyChanged(nameof(TextColor));
        }
    }
}
