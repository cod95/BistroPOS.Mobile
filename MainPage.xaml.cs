using BistroPOS.Mobile.Services;

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

        await UpdatePendingCountAsync();
    }

    private async Task UpdatePendingCountAsync()
    {
        var pending = await _api.GetOrdersAsync("Pending");
        int count = pending?.Count ?? 0;
        OrdersButton.Text = count > 0 ? $"الطلبات ({count})" : "الطلبات";
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
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
