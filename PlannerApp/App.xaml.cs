using PlannerApp.Services;

namespace PlannerApp
{
    public partial class App : Application
    {
        private readonly DatabaseService _db;
        private readonly NotificationService _notif;

        public App(DatabaseService db, NotificationService notif)
        {
            InitializeComponent();
            _db = db;
            _notif = notif;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();
            try
            {
                await _db.InitializeAsync();
                await _notif.RequestPermissionAsync();
                await _notif.ScheduleDailyNotificationsAsync(DateTime.Today);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnStart error: {ex}");
            }
        }
    }
}
