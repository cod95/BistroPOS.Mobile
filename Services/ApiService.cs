using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace BistroPOS.Mobile.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService()
        {
            // ✅ الـ IP الصحيح لجهاز الكمبيوتر
            _baseUrl = "http://192.168.100.32:5050";

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl)
            };
        }

        /// <summary>
        /// اختبار الاتصال بالسيرفر
        /// </summary>
        public async Task<bool> PingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// تسجيل الدخول
        /// </summary>
        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync("/api/login", request);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<LoginResponse>();
            }
            catch
            {
                return null;
            }
        }

        // يمكنك إضافة دوال أخرى هنا مستقبلاً
        // مثل: GetProductsAsync(), CreateOrderAsync(), etc.
    }

    // ==========================================
    // نماذج البيانات (Models)
    // ==========================================

    public class LoginResponse
    {
        public bool Success { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}