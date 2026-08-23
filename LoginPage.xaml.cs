using BistroPOS.Mobile.Services;
namespace BistroPOS.Mobile;
public partial class LoginPage : ContentPage
{
    private readonly ApiService _api = new();

    public LoginPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ConnectToServerAsync();
    }

    private async Task ConnectToServerAsync()
    {
        ConnectionStatusLabel.Text = "جاري البحث عن السيرفر...";
        ConnectionStatusLabel.TextColor = Colors.Gray;

        bool found = await _api.DiscoverServerAsync();

        if (found)
        {
            ConnectionStatusLabel.Text = $"متصل بالسيرفر ({_api.BaseUrl})";
            ConnectionStatusLabel.TextColor = Color.FromArgb("#1E8C3C");
            return;
        }

        bool reachable = await _api.PingAsync();
        if (reachable)
        {
            ConnectionStatusLabel.Text = $"متصل (آخر عنوان معروف: {_api.BaseUrl})";
            ConnectionStatusLabel.TextColor = Color.FromArgb("#1E8C3C");
        }
        else
        {
            ConnectionStatusLabel.Text = "ما قدرنا نوصل للسيرفر تلقائياً — دوسي 'تغيير عنوان السيرفر' تحت";
            ConnectionStatusLabel.TextColor = Color.FromArgb("#A01E1E");
        }
    }

    private void OnChangeServerTapped(object sender, EventArgs e)
    {
        ManualServerPanel.IsVisible = !ManualServerPanel.IsVisible;
    }

    private async void OnSaveManualIpClicked(object sender, EventArgs e)
    {
        string ip = ManualIpEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(ip))
        {
            await DisplayAlert("تنبيه", "اكتب عنوان الـ IP", "حسناً");
            return;
        }

        _api.SetManualServerIp(ip);
        ConnectionStatusLabel.Text = "جاري التحقق...";
        ConnectionStatusLabel.TextColor = Colors.Gray;

        bool reachable = await _api.PingAsync();
        if (reachable)
        {
            ConnectionStatusLabel.Text = $"متصل بنجاح ({_api.BaseUrl})";
            ConnectionStatusLabel.TextColor = Color.FromArgb("#1E8C3C");
            ManualServerPanel.IsVisible = false;
        }
        else
        {
            ConnectionStatusLabel.Text = "ما قدرنا نتصل بهاد العنوان، تأكدي منه";
            ConnectionStatusLabel.TextColor = Color.FromArgb("#A01E1E");
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("تنبيه", "الرجاء إدخال اسم المستخدم وكلمة المرور", "حسناً");
            return;
        }
        try
        {
            var result = await _api.LoginAsync(username, password);
            if (result != null && result.Success)
            {
                Preferences.Set("Username", result.Username);
                Preferences.Set("FullName", result.FullName);
                Preferences.Set("Role", result.Role);
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                await DisplayAlert("خطأ", "اسم المستخدم أو كلمة المرور خطأ", "حاول مجدداً");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("مشكلة تقنية", $"الخطأ الحقيقي: {ex.Message}", "حسناً");
        }
    }
}
