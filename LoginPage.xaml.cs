using BistroPOS.Mobile.Services;
using Microsoft.Maui.Controls;

namespace BistroPOS.Mobile
{
    public partial class LoginPage : ContentPage
    {
        private readonly ApiService _api;

        public LoginPage()
        {
            InitializeComponent();
            _api = new ApiService();
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            var username = UsernameEntry.Text;
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Œÿ√", "«·—Ã«¡ ≈œŒ«· «”„ «·„” Œœ„ Êﬂ·„… «·„—Ê—", "Õ”‰");
                return;
            }

            var user = await _api.LoginAsync(username, password);

            if (user == null || !user.Success)
            {
                await DisplayAlert("Œÿ√", "«”„ «·„” Œœ„ √Ê ﬂ·„… «·„—Ê— €Ì— ’ÕÌÕ…", "Õ”‰");
                return;
            }

            Preferences.Set("Username", user.Username);
            Preferences.Set("FullName", user.FullName);
            Preferences.Set("Role", user.Role);

            await DisplayAlert("‰ÃÕ", " ”ÃÌ· «·œŒÊ· ‰«ÃÕ! (·”« „« ›Ì ‘«‘… —∆Ì”Ì…)", " „«„");
        }
    }
}