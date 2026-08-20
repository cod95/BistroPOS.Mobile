using BistroPOS.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace BistroPOS.Mobile
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

            // إضافة خدمات التطبيق
            builder.Services.AddSingleton<ApiService>();

            return builder.Build();
        }
    }
}
