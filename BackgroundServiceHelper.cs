namespace BistroPOS.Mobile;

public static class BackgroundServiceHelper
{
    public static void StartOrderPollingService()
    {
#if ANDROID
        var context = global::Android.App.Application.Context;
        var intent = new global::Android.Content.Intent(context, typeof(Platforms.Android.OrderPollingService));
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
#endif
    }

    public static void StopOrderPollingService()
    {
#if ANDROID
        var context = global::Android.App.Application.Context;
        var intent = new global::Android.Content.Intent(context, typeof(Platforms.Android.OrderPollingService));
        context.StopService(intent);
#endif
    }

    public static async Task RequestBatteryOptimizationExemptionAsync()
    {
#if ANDROID
        try
        {
            var context = global::Android.App.Application.Context;
            var packageName = context.PackageName;
            var powerManager = (global::Android.OS.PowerManager?)context.GetSystemService(global::Android.Content.Context.PowerService);

            if (powerManager != null && !powerManager.IsIgnoringBatteryOptimizations(packageName))
            {
                var intent = new global::Android.Content.Intent(
                    global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(global::Android.Net.Uri.Parse($"package:{packageName}"));
                intent.SetFlags(global::Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch { }
        await Task.CompletedTask;
#endif
    }
}
