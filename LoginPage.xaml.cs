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
        // محاولة تسجيل الدخول مع حماية من الأخطاء (Try-Catch)
        try
        {
            var api = new ApiService();
            var result = await api.LoginAsync(username, password);
            if (result != null && result.Success)
            {
                await DisplayAlert("نجاح", $"مرحباً {result.FullName}", "حسناً");
                // تم حذف سطر //MainPage نهائياً
            }
            else
            {
                await DisplayAlert("خطأ", "اسم المستخدم أو كلمة المرور خطأ", "حاول مجدداً");
            }
        }
        catch (Exception ex)
        {
            // مؤقتاً: نعرض الخطأ الحقيقي عشان نشخص المشكلة
            await DisplayAlert("مشكلة تقنية", $"الخطأ الحقيقي: {ex.Message}", "حسناً");
        }
    }
}
