using PlannerApp.ViewModels;

namespace PlannerApp.Views
{
    public partial class WeekPage : ContentPage
    {
        private readonly WeekViewModel _vm;

        public WeekPage(WeekViewModel vm)
        {
            InitializeComponent();
            BindingContext = _vm = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _vm.LoadAsync();
        }
    }
}
