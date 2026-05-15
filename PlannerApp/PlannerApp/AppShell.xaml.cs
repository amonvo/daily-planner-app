using PlannerApp.Views;

namespace PlannerApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("daydetail", typeof(DayDetailPage));
        }
    }
}
