using BistroPOS.Mobile.Services;
namespace BistroPOS.Mobile;
public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
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
            var api = new ApiService();
            var result = await api.LoginAsync(username, password);
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
