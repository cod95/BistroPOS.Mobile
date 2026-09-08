using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using BistroPOS.Mobile.Services;
using Plugin.LocalNotification;

namespace BistroPOS.Mobile.Platforms.Android
{
    [Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync)]
    public class OrderPollingService : Service
    {
        private const int ServiceNotificationId = 9001;
        private const string ChannelId = "bistropos_service_channel";
        private System.Threading.CancellationTokenSource? _cts;
        private ApiService? _api;

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            if (_cts != null)
                return StartCommandResult.Sticky; // الخدمة أصلاً شغالة، ما منبلشها مرتين

            CreateNotificationChannel();
            StartForeground(ServiceNotificationId, BuildServiceNotification());

            _api = new ApiService();
            _cts = new System.Threading.CancellationTokenSource();
            _ = PollLoopAsync(_cts.Token);

            return StartCommandResult.Sticky; // لو أندرويد قتلها لسبب ما، يعيد تشغيلها تلقائياً
        }

        public override void OnDestroy()
        {
            _cts?.Cancel();
            _cts = null;
            base.OnDestroy();
        }

        private async Task PollLoopAsync(System.Threading.CancellationToken token)
        {
            var startTime = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await CheckForNewOrdersAsync();
                }
                catch { }

                // قبل ما توصل لسقف الـ٦ ساعات (أندرويد ١٥)، نعيد تشغيل الخدمة من جديد بشكل سلس
                if (DateTime.UtcNow - startTime > TimeSpan.FromHours(5.5))
                {
                    RestartSelf();
                    return;
                }

                try { await Task.Delay(TimeSpan.FromSeconds(15), token); }
                catch (TaskCanceledException) { }
            }
        }

        private void RestartSelf()
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(OrderPollingService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }

        private async Task CheckForNewOrdersAsync()
        {
            if (_api == null) return;

            string token = Preferences.Get("ApiToken", "");
            if (string.IsNullOrWhiteSpace(token)) return; // مسجل خروج، ما في داعي نفحص

            var all = await _api.GetOrdersAsync("All");
            if (all == null) return;

            string notifiedRaw = Preferences.Get("NotifiedOrderIds", "");
            var notified = notifiedRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => int.TryParse(s, out int v) ? v : -1)
                               .Where(v => v > 0)
                               .ToHashSet();

            bool isFirstRun = !Preferences.Get("NotificationBaselineSet", false);
            bool changed = false;

            foreach (var order in all)
            {
                if (order.Status == "Cancelled") continue;
                if (!notified.Contains(order.OrderId))
                {
                    if (!isFirstRun)
                        await ShowOrderNotificationAsync(order);
                    notified.Add(order.OrderId);
                    changed = true;
                }
            }

            if (isFirstRun)
                Preferences.Set("NotificationBaselineSet", true);

            if (changed)
                Preferences.Set("NotifiedOrderIds", string.Join(",", notified));
        }

        private async Task ShowOrderNotificationAsync(OrderDto order)
        {
            try
            {
                string tableInfo = (!string.IsNullOrWhiteSpace(order.TableNumber) && order.TableNumber != "طلبية موبايل")
                    ? $" - {order.TableNumber}" : "";
                string itemsText = string.Join(", ", order.Items.Select(i => $"{i.Name} {i.Quantity}"));

                string statusLabel = order.Status switch
                {
                    "Completed" => "مكتملة (مدفوعة)",
                    "Pending" => "جديدة",
                    "Preparing" => "قيد التحضير",
                    "Ready" => "جاهزة",
                    _ => order.Status
                };

                var request = new NotificationRequest
                {
                    NotificationId = order.OrderId,
                    Title = $"طلبية {statusLabel} #{order.OrderId:D4}{tableInfo}",
                    Description = itemsText
                };

                await LocalNotificationCenter.Current.Show(request);
            }
            catch { }
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;

            var channel = new NotificationChannel(ChannelId, "Bistro POS - خدمة الاتصال",
                NotificationImportance.Low)
            {
                Description = "إشعار دائم يشير إلى أن التطبيق متصل ويستقبل الطلبيات"
            };

            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }

        private Notification BuildServiceNotification()
        {
            var builder = new NotificationCompat.Builder(this, ChannelId)
                .SetContentTitle("Bistro POS")
                .SetContentText("متصل ويستقبل الطلبيات الجديدة")
                .SetSmallIcon(_Microsoft.Android.Resource.Designer.ResourceConstant.Mipmap.appicon)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityLow);

            return builder.Build();
        }
    }
}
