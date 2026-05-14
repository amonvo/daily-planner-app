using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using PlannerApp.Models;

namespace PlannerApp.Services
{
    public class NotificationService
    {
        private readonly DatabaseService _db;

        public const string ChannelId = "plannerapp_channel";

        public NotificationService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<bool> RequestPermissionAsync()
        {
            try
            {
                if (await LocalNotificationCenter.Current.AreNotificationsEnabled())
                    return true;
                return await LocalNotificationCenter.Current.RequestNotificationPermission();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RequestPermission error: {ex}");
                return false;
            }
        }

        public async Task ScheduleDailyNotificationsAsync(DateTime date)
        {
            try
            {
                var blocks = await _db.GetScheduleBlocksForDateAsync(date).ConfigureAwait(false);
                var now = DateTime.Now;
                var dayOfYear = date.DayOfYear;

                foreach (var block in blocks)
                {
                    var fireTime = date.Date.Add(block.TimeFrom);
                    if (fireTime <= now) continue;

                    var id = dayOfYear * 100 + block.Id;
                    var notif = new NotificationRequest
                    {
                        NotificationId = id,
                        Title = "PlannerApp – èas zmìnit aktivitu",
                        Description = block.NotificationMessage,
                        Schedule = new NotificationRequestSchedule
                        {
                            NotifyTime = fireTime
                        },
                        Android = new AndroidOptions
                        {
                            ChannelId = ChannelId,
                            Priority = AndroidPriority.High
                        }
                    };

                    await LocalNotificationCenter.Current.Show(notif);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ScheduleDaily error: {ex}");
            }
        }

        public Task CancelAllNotificationsAsync()
        {
            try
            {
                LocalNotificationCenter.Current.CancelAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CancelAll error: {ex}");
            }
            return Task.CompletedTask;
        }

        public async Task RescheduleForTodayAsync()
        {
            await CancelAllNotificationsAsync().ConfigureAwait(false);
            await ScheduleDailyNotificationsAsync(DateTime.Today).ConfigureAwait(false);
        }
    }
}
