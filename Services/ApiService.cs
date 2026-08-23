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

        public string? LastError { get; private set; }

        public ApiService()
        {
            _baseUrl = "http://192.168.0.122:5050";
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

        public async Task<List<OrderDto>?> GetOrdersAsync(string status = "All", DateTime? date = null)
        {
            try
            {
                string url = $"/api/orders?status={Uri.EscapeDataString(status)}";
                if (date.HasValue)
                    url += $"&date={date.Value:yyyy-MM-dd}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    LastError = $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}";
                    return null;
                }
                var result = await response.Content.ReadFromJsonAsync<OrdersResponse>();
                LastError = null;
                return result?.Orders;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }

        public async Task<AdvanceOrderResponse?> AdvanceOrderAsync(int orderId)
        {
            try
            {
                var request = new { orderId };
                var response = await _httpClient.PostAsJsonAsync("/api/order/advance", request);
                return await response.Content.ReadFromJsonAsync<AdvanceOrderResponse>();
            }
            catch { return null; }
        }

        public async Task<DeleteOrderResponse?> DeleteOrderAsync(int orderId)
        {
            try
            {
                var request = new { orderId };
                var response = await _httpClient.PostAsJsonAsync("/api/order/delete", request);
                return await response.Content.ReadFromJsonAsync<DeleteOrderResponse>();
            }
            catch { return null; }
        }

        public async Task<DeliverAllResponse?> DeliverAllAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/orders/deliver-all", null);
                return await response.Content.ReadFromJsonAsync<DeliverAllResponse>();
            }
            catch { return null; }
        }

        public async Task<bool> NewDayAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/orders/new-day", null);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
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
        public decimal Discount { get; set; }
        public List<OrderItemRequest> Items { get; set; } = new();
    }

    public class CreateOrderResponse
    {
        public bool Success { get; set; }
        public int OrderId { get; set; }
        public string? Message { get; set; }
    }

    public class OrderItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class OrderDto
    {
        public int OrderId { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OrderTime { get; set; } = string.Empty;
    }

    public class OrdersResponse
    {
        public bool Success { get; set; }
        public List<OrderDto> Orders { get; set; } = new();
    }

    public class AdvanceOrderResponse
    {
        public bool Success { get; set; }
        public string? NewStatus { get; set; }
        public string? Message { get; set; }
    }

    public class DeleteOrderResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class DeliverAllResponse
    {
        public bool Success { get; set; }
        public int Count { get; set; }
        public decimal Total { get; set; }
    }
}
