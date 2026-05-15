using System.Globalization;
using PlannerApp.ViewModels;

namespace PlannerApp.Views
{
    [QueryProperty(nameof(DateString), "date")]
    public partial class DayDetailPage : ContentPage
    {
        private readonly DayDetailViewModel _vm;
        private string? _dateString;
        private DateTime _parsedDate = DateTime.Today;

        public DayDetailPage(DayDetailViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
        }

        public string? DateString
        {
            get => _dateString;
            set
            {
                _dateString = value;
                if (!string.IsNullOrWhiteSpace(value) &&
                    DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                {
                    _parsedDate = d.Date;
                }
                else
                {
                    _parsedDate = DateTime.Today;
                }
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadDayAsync(_parsedDate);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _vm.StopTimer();
        }
    }
}
