namespace BistroPOS.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var fullName = Preferences.Get("FullName", "");
        var role = Preferences.Get("Role", "");
        WelcomeLabel.Text = $"مرحباً {fullName}";
        RoleLabel.Text = $"الدور: {role}";
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        Preferences.Clear();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
