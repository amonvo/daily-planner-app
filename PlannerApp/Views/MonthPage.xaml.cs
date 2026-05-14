using PlannerApp.ViewModels;

namespace PlannerApp.Views
{
    public partial class MonthPage : ContentPage
    {
        private readonly MonthViewModel _vm;

        public MonthPage(MonthViewModel vm)
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
