using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BistroPOS.Mobile.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient = new();
        private const int DiscoveryPort = 5051;
        private const int ApiPort = 5050;

        public string? LastError { get; private set; }
        public string BaseUrl { get; private set; }

        public ApiService()
        {
            string savedIp = Preferences.Get("ServerIp", "192.168.0.122");
            BaseUrl = $"http://{savedIp}:{ApiPort}";
            _httpClient.BaseAddress = new Uri(BaseUrl);

            string token = Preferences.Get("ApiToken", "");
            if (!string.IsNullOrWhiteSpace(token))
                _httpClient.DefaultRequestHeaders.Add("X-Api-Token", token);
        }

        public async Task<bool> DiscoverServerAsync()
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;

                byte[] message = Encoding.UTF8.GetBytes("BISTROPOS_DISCOVER");
                var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                await udp.SendAsync(message, message.Length, broadcastEndpoint);

                var receiveTask = udp.ReceiveAsync();
                var timeoutTask = Task.Delay(2500);
                var completed = await Task.WhenAny(receiveTask, timeoutTask);

                if (completed == receiveTask)
                {
                    var result = receiveTask.Result;
                    string reply = Encoding.UTF8.GetString(result.Buffer);
                    if (reply == "BISTROPOS_HERE")
                    {
                        string ip = result.RemoteEndPoint.Address.ToString();
                        Preferences.Set("ServerIp", ip);
                        BaseUrl = $"http://{ip}:{ApiPort}";
                        _httpClient.BaseAddress = new Uri(BaseUrl);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public void SetManualServerIp(string ip)
        {
            Preferences.Set("ServerIp", ip);
            BaseUrl = $"http://{ip}:{ApiPort}";
            _httpClient.BaseAddress = new Uri(BaseUrl);
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

        public async Task<ReportsDto?> GetReportsAsync(DateTime from, DateTime to)
        {
            try
            {
                string url = $"/api/reports?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<ReportsDto>();
            }
            catch { return null; }
        }

        public async Task<List<DebtCustomerDto>?> GetDebtCustomersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/debts/customers");
                if (!response.IsSuccessStatusCode) return null;
                var result = await response.Content.ReadFromJsonAsync<DebtCustomersResponse>();
                return result?.Customers;
            }
            catch { return null; }
        }

        public async Task<CustomerDebtsResponse?> GetCustomerDebtsAsync(string name)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/debts/customer?name={Uri.EscapeDataString(name)}");
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<CustomerDebtsResponse>();
            }
            catch { return null; }
        }

        public async Task<SimpleResponse?> AddDebtCustomerAsync(string name)
        {
            try
            {
                var request = new { name };
                var response = await _httpClient.PostAsJsonAsync("/api/debts/add-customer", request);
                return await response.Content.ReadFromJsonAsync<SimpleResponse>();
            }
            catch { return null; }
        }

        public async Task<SimpleResponse?> DeleteDebtCustomerAsync(string name)
        {
            try
            {
                var request = new { name };
                var response = await _httpClient.PostAsJsonAsync("/api/debts/delete-customer", request);
                return await response.Content.ReadFromJsonAsync<SimpleResponse>();
            }
            catch { return null; }
        }

        public async Task<SimpleResponse?> PayDebtAsync(string customerName, decimal amount)
        {
            try
            {
                var request = new { customerName, amount };
                var response = await _httpClient.PostAsJsonAsync("/api/debts/pay", request);
                return await response.Content.ReadFromJsonAsync<SimpleResponse>();
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
        public string Token { get; set; } = string.Empty;
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

    public class CategorySalesDto
    {
        public string Category { get; set; } = string.Empty;
        public int TotalQty { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TopItemDto
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalSold { get; set; }
        public decimal Revenue { get; set; }
    }

    public class ReportsDto
    {
        public bool Success { get; set; }
        public int TotalOrders { get; set; }
        public decimal Revenue { get; set; }
        public decimal AvgOrder { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public List<CategorySalesDto> SalesByCategory { get; set; } = new();
        public List<TopItemDto> TopItems { get; set; } = new();
    }

    public class DebtCustomerDto
    {
        public string Name { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Remaining { get; set; }
    }

    public class DebtCustomersResponse
    {
        public bool Success { get; set; }
        public List<DebtCustomerDto> Customers { get; set; } = new();
    }

    public class DebtItemDto
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public class CustomerDebtDto
    {
        public int Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public List<DebtItemDto> Items { get; set; } = new();
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
        public decimal Remaining { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerDebtsResponse
    {
        public bool Success { get; set; }
        public List<CustomerDebtDto> Debts { get; set; } = new();
        public decimal TotalRemaining { get; set; }
    }

    public class SimpleResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}
