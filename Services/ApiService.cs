using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace BistroPOS.Mobile.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService()
        {
            _baseUrl = "http://192.168.100.32:5050";
            _httpClient = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/ping");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<LoginResponse?> LoginAsync(string username, string password)
        {
            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync("/api/login", request);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<LoginResponse>();
            }
            catch { return null; }
        }

        // ============ جديد: جلب المينيو ============
        public async Task<List<MenuItemDto>?> GetMenuAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/menu");
                if (!response.IsSuccessStatusCode) return null;
                var result = await response.Content.ReadFromJsonAsync<MenuResponse>();
                return result?.Items;
            }
            catch { return null; }
        }

        // ============ جديد: إرسال طلبية ============
        public async Task<CreateOrderResponse?> CreateOrderAsync(CreateOrderRequest order)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/order", order);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
            }
            catch { return null; }
        }
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

    public class MenuItemDto
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class MenuResponse
    {
        public bool Success { get; set; }
        public List<MenuItemDto> Items { get; set; } = new();
    }

    public class OrderItemRequest
    {
        public int ItemID { get; set; }
        public int Quantity { get; set; }
    }

    public class CreateOrderRequest
    {
        public string? TableNumber { get; set; }
        public string? Notes { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderResponse
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string? Message { get; set; }
    }
}
