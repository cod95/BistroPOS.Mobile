using BistroPOS.Mobile.Services;
using Plugin.LocalNotification;

namespace BistroPOS.Mobile;

public partial class MainPage : ContentPage
{
    private readonly ApiService _api = new();

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

    private async Task CheckForNewOrdersAsync()
    {
        var all = await _api.GetOrdersAsync("All");
        if (all == null) return;
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

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        BackgroundServiceHelper.StopOrderPollingService();
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
        private async void OnDebtsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//DebtsPage");
    }
}
