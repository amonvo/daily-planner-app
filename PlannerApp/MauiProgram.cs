using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using PlannerApp.Services;
using PlannerApp.ViewModels;
using PlannerApp.Views;

namespace PlannerApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseLocalNotification(config =>
                {
                    config.AddAndroid(android =>
                    {
                        android.AddChannel(new Plugin.LocalNotification.AndroidOption.NotificationChannelRequest
                        {
                            Id = NotificationService.ChannelId,
                            Name = "PlannerApp připomenutí",
                            Importance = Plugin.LocalNotification.AndroidOption.AndroidImportance.High,
                            Description = "Připomenutí aktivit z denního plánu."
                        });
                    });
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<DatabaseService>();
            builder.Services.AddSingleton<NotificationService>();

            builder.Services.AddTransient<TodayViewModel>();
            builder.Services.AddTransient<WeekViewModel>();
            builder.Services.AddTransient<MonthViewModel>();
            builder.Services.AddTransient<YearViewModel>();

            builder.Services.AddTransient<TodayPage>();
            builder.Services.AddTransient<WeekPage>();
            builder.Services.AddTransient<MonthPage>();
            builder.Services.AddTransient<YearPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

