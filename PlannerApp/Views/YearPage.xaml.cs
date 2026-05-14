using PlannerApp.ViewModels;

namespace PlannerApp.Views
{
    public partial class YearPage : ContentPage
    {
        private readonly YearViewModel _vm;

        public YearPage(YearViewModel vm)
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
