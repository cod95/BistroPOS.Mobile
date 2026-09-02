using BistroPOS.Mobile.Services;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Plugin.LocalNotification.AndroidOption;
using BarcodeScanning;

namespace BistroPOS.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseLocalNotification(config =>
                {
                    config.AddAndroid(android =>
                    {
                        android.AddChannel(new NotificationChannelRequest
                        {
                            Id = "orders",
                            Name = "الطلبات",
                            Description = "إشعارات الطلبات الجديدة"
                        });
                    });
                })
                .UseBarcodeScanning()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            // إضافة خدمات التطبيق
            builder.Services.AddSingleton<ApiService>();
            return builder.Build();
        }
    }
}
