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

        await UpdatePendingCountAsync();
        await RequestNotificationPermissionAsync();
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
        _pollTimer.Interval = TimeSpan.FromSeconds(20);
        _pollTimer.Tick += async (s, e) => await CheckForNewOrdersAsync();
        _pollTimer.Start();
    }

    private async Task CheckForNewOrdersAsync()
    {
        var pending = await _api.GetOrdersAsync("Pending");
        if (pending == null) return;

        string raw = Preferences.Get("NotifiedOrderIds", "");
        var notified = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                           .Select(s => int.TryParse(s, out int v) ? v : -1)
                           .Where(v => v > 0)
                           .ToHashSet();

        bool changed = false;
        foreach (var order in pending)
        {
            if (!notified.Contains(order.OrderId))
            {
                await ShowOrderNotificationAsync(order);
                notified.Add(order.OrderId);
                changed = true;
            }
        }

        if (changed)
            Preferences.Set("NotifiedOrderIds", string.Join(",", notified));

        OrdersButton.Text = pending.Count > 0 ? $"الطلبات ({pending.Count})" : "الطلبات";
    }

    private async Task ShowOrderNotificationAsync(OrderDto order)
    {
        try
        {
            string tableInfo = (!string.IsNullOrWhiteSpace(order.TableNumber) && order.TableNumber != "طلبية موبايل")
                ? $" - {order.TableNumber}" : "";
            string itemsText = string.Join(", ", order.Items.Select(i => $"{i.Name} {i.Quantity}"));

            var request = new NotificationRequest
            {
                NotificationId = order.OrderId,
                Title = $"طلبية جديدة #{order.OrderId:D4}{tableInfo}",
                Description = itemsText
            };

            await LocalNotificationCenter.Current.Show(request);
        }
        catch { }
    }

    private async Task UpdatePendingCountAsync()
    {
        var pending = await _api.GetOrdersAsync("Pending");
        int count = pending?.Count ?? 0;
        OrdersButton.Text = count > 0 ? $"الطلبات ({count})" : "الطلبات";
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
}
