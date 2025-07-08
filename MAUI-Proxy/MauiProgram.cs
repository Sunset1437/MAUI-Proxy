using Android.App;
using Android.OS;
using MAUI_Proxy.Converters;
using MAUI_Proxy.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

namespace MAUI_Proxy
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Регистрация сервисов
            builder.Services.AddSingleton<ProxyViewModel>();
            builder.Services.AddTransient<MainPage>();

            // Регистрация конвертеров
            builder.Services.AddSingleton<IValueConverter, ConnectionButtonTextConverter>();
            builder.Services.AddSingleton<IValueConverter, InverseBooleanConverter>();

#if ANDROID
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android => android
                    .OnCreate((activity, bundle) =>
                    {
                        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                        {
                            var channel = new NotificationChannel(
                                "vpn_channel",
                                "VPN Service",
                                NotificationImportance.Default);

                            var notificationManager = (NotificationManager)activity.GetSystemService(Android.Content.Context.NotificationService);
                            notificationManager?.CreateNotificationChannel(channel);
                        }
                    }));
            });
#endif

            return builder.Build();
        }
    }
}
