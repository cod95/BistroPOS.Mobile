using BistroPOS.Mobile.Services;
using Plugin.LocalNotification;

namespace BistroPOS.Mobile;

public partial class MainPage : ContentPage
{
    private readonly ApiService _api = new();
    private IDispatcherTimer? _pollTimer;
    private bool _isPollingStarted;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var fullName = Preferences.Get("FullName", "");
        var role = Preferences.Get("Role", "");
        WelcomeLabel.Text = $"مرحباً {fullName}";
        RoleLabel.Text = $"الدور: {role}";
        ReportsButton.IsVisible = role == "Admin";
        DebtsButton.IsVisible = role == "Admin";

        await RequestNotificationPermissionAsync();
        await CheckForNewOrdersAsync();
        StartNotificationPolling();
    }

    private async Task RequestNotificationPermissionAsync()
    {
        try
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
                await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch { }
    }

    private void StartNotificationPolling()
    {
        if (_isPollingStarted) return;
        _isPollingStarted = true;

        _pollTimer = Dispatcher.CreateTimer();
        _pollTimer.Interval = TimeSpan.FromSeconds(5);
        _pollTimer.Tick += async (s, e) => await CheckForNewOrdersAsync();
        _pollTimer.Start();
    }

    private async Task CheckForNewOrdersAsync()
    {
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

        UpdateUnseenBadge(all);
    }

    private void UpdateUnseenBadge(List<OrderDto> all)
    {
        string seenRaw = Preferences.Get("SeenOrderIds", "");
        var seen = seenRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => int.TryParse(s, out int v) ? v : -1)
                           .Where(v => v > 0)
                           .ToHashSet();

        int unseenCount = all.Count(o => o.Status != "Cancelled" && !seen.Contains(o.OrderId));
        OrdersButton.Text = unseenCount > 0 ? $"الطلبات ({unseenCount})" : "الطلبات";
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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        _pollTimer?.Stop();
        Preferences.Clear();
        await Shell.Current.GoToAsync("//LoginPage");
    }

    private async void OnNewOrderClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//OrderPage");
    }

    private async void OnOrdersClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//OrdersPage");
    }

    private async void OnReportsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ReportsPage");
    }
}
