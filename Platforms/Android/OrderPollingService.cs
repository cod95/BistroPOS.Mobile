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
