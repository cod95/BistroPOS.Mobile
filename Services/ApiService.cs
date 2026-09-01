using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestaurantOMS.Data;
using RestaurantOMS.Models;

namespace RestaurantOMS.Api
{
    public static class ApiServer
    {
        private static HttpListener _listener;
        private static CancellationTokenSource _cts;
        private const int Port = 5050;

        private static UdpClient _discoveryListener;
        private const int DiscoveryPort = 5051;

        private static readonly Dictionary<string, string> _tokenRoles = new();
        private static readonly Dictionary<string, string> _tokenUsers = new();
        private static readonly object _tokenLock = new();

        public static void Start()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Start();
                Task.Run(() => Listen(_cts.Token));
                Console.WriteLine($"[ApiServer] Started on port {Port}");

                StartDiscoveryListener();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ApiServer] Failed to start: " + ex.Message);
            }
        }

        public static void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _discoveryListener?.Close();
            }
            catch { }
        }

        private static void StartDiscoveryListener()
        {
            try
            {
                _discoveryListener = new UdpClient(DiscoveryPort);
                _discoveryListener.EnableBroadcast = true;
                Task.Run(ListenForDiscovery);
                Console.WriteLine($"[Discovery] Listening on port {DiscoveryPort}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Discovery] Failed to start: " + ex.Message);
            }
        }

        private static async Task ListenForDiscovery()
        {
            while (true)
            {
                try
                {
                    var result = await _discoveryListener.ReceiveAsync();
                    string message = Encoding.UTF8.GetString(result.Buffer);

                    if (message == "BISTROPOS_DISCOVER")
                    {
                        byte[] reply = Encoding.UTF8.GetBytes("BISTROPOS_HERE");
                        await _discoveryListener.SendAsync(reply, reply.Length, result.RemoteEndPoint);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Discovery] Listen error: " + ex.Message);
                    break;
                }
            }
        }

        private static async Task Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ApiServer] Listen error: " + ex.Message);
                }
            }
        }

        private static bool IsAuthorized(HttpListenerRequest request, out string role, out string fullName)
        {
            role = null; fullName = null;
            string token = request.Headers["X-Api-Token"];
            if (string.IsNullOrWhiteSpace(token)) return false;
            lock (_tokenLock)
            {
                if (!_tokenRoles.TryGetValue(token, out role)) return false;
                _tokenUsers.TryGetValue(token, out fullName);
                return true;
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            response.AddHeader("Access-Control-Allow-Origin", "*");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Api-Token");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");

            try
            {
                string path = request.Url.AbsolutePath.ToLower();

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                string userRole = null;
                string userFullName = null;

                if (path != "/api/login" && path != "/api/ping")
                {
                    if (!IsAuthorized(request, out userRole, out userFullName))
                    {
                        WriteJson(response, 401, new { success = false, message = "غير مصرح — الرجاء تسجيل الدخول من جديد" });
                        return;
                    }

                    if (path == "/api/orders/new-day" && userRole != "Admin")
                    {
                        WriteJson(response, 403, new { success = false, message = "هذا الإجراء متاح للمدير فقط" });
                        return;
                    }

                    if (path == "/api/order/delete" && userRole != "Admin")
                    {
                        WriteJson(response, 403, new { success = false, message = "هذا الإجراء متاح للمدير فقط" });
                        return;
                    }

                    if (path == "/api/reports" && userRole != "Admin")
                    {
                        WriteJson(response, 403, new { success = false, message = "هذا الإجراء متاح للمدير فقط" });
                        return;
                    }

                    if (path.StartsWith("/api/debts") && userRole != "Admin")
                    {
                        WriteJson(response, 403, new { success = false, message = "هذا الإجراء متاح للمدير فقط" });
                        return;
                    }
                }

                if (path == "/api/login" && request.HttpMethod == "POST")
                {
                    HandleLogin(request, response);
                }
                else if (path == "/api/ping" && request.HttpMethod == "GET")
                {
                    WriteJson(response, 200, new { success = true, message = "Bistro POS API is running" });
                }
                else if (path == "/api/menu" && request.HttpMethod == "GET")
                {
                    HandleGetMenu(response);
                }
                else if (path == "/api/order" && request.HttpMethod == "POST")
                {
                    HandleCreateOrder(request, response, userFullName);
                }
                else if (path == "/api/orders" && request.HttpMethod == "GET")
                {
                    HandleGetOrders(request, response);
                }
                else if (path == "/api/order/advance" && request.HttpMethod == "POST")
                {
                    HandleAdvanceOrder(request, response);
                }
                else if (path == "/api/order/delete" && request.HttpMethod == "POST")
                {
                    HandleDeleteOrder(request, response);
                }
                else if (path == "/api/orders/deliver-all" && request.HttpMethod == "POST")
                {
                    HandleDeliverAll(response);
                }
                else if (path == "/api/orders/new-day" && request.HttpMethod == "POST")
                {
                    HandleNewDay(response);
                }
                else if (path == "/api/reports" && request.HttpMethod == "GET")
                {
                    HandleGetReports(request, response);
                }
                else if (path == "/api/debts/customers" && request.HttpMethod == "GET")
                {
                    HandleGetDebtCustomers(response);
                }
                else if (path == "/api/debts/customer" && request.HttpMethod == "GET")
                {
                    HandleGetCustomerDebts(request, response);
                }
                else if (path == "/api/debts/add-customer" && request.HttpMethod == "POST")
                {
                    HandleAddDebtCustomer(request, response);
                }
                else if (path == "/api/debts/delete-customer" && request.HttpMethod == "POST")
                {
                    HandleDeleteDebtCustomer(request, response);
                }
                else if (path == "/api/debts/pay" && request.HttpMethod == "POST")
                {
                    HandlePayDebt(request, response);
                }
                else
                {
                    WriteJson(response, 404, new { success = false, message = "Endpoint not found" });
                }
            }
            catch (Exception ex)
            {
                WriteJson(response, 500, new { success = false, message = "Server error: " + ex.Message });
            }
        }

        private class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private static void HandleLogin(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var login = JsonConvert.DeserializeObject<LoginRequest>(body);

            if (login == null || string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrWhiteSpace(login.Password))
            {
                WriteJson(response, 400, new { success = false, message = "Username and password required" });
                return;
            }

            var user = DatabaseHelper.AuthenticateUser(login.Username.Trim(), login.Password);

            if (user == null)
            {
                WriteJson(response, 401, new { success = false, message = "Invalid username or password" });
                return;
            }

            string token = Guid.NewGuid().ToString("N");
            lock (_tokenLock)
            {
                _tokenRoles[token] = user.Role;
                _tokenUsers[token] = user.FullName;
            }

            WriteJson(response, 200, new
            {
                success = true,
                userId = user.UserID,
                username = user.Username,
                fullName = user.FullName,
                role = user.Role,
                token = token
            });
        }

        private static void HandleGetMenu(HttpListenerResponse response)
        {
            var items = DatabaseHelper.GetAllMenuItems(availableOnly: true);

            var result = new List<object>();
            foreach (var item in items)
            {
                result.Add(new
                {
                    itemId = item.ItemID,
                    name = item.Name,
                    category = item.Category,
                    price = item.Price
                });
            }

            WriteJson(response, 200, new { success = true, items = result });
        }

        private class OrderItemRequest
        {
            public int ItemID { get; set; }
            public int Quantity { get; set; }
        }

        private class OrderRequest
        {
            public string TableNumber { get; set; }
            public string Notes { get; set; }
            public decimal Discount { get; set; }
            public List<OrderItemRequest> Items { get; set; }
        }

        private static void HandleCreateOrder(HttpListenerRequest request, HttpListenerResponse response, string createdBy)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var orderReq = JsonConvert.DeserializeObject<OrderRequest>(body);

            if (orderReq == null || orderReq.Items == null || orderReq.Items.Count == 0)
            {
                WriteJson(response, 400, new { success = false, message = "الطلبية لازم تحتوي على صنف واحد عالأقل" });
                return;
            }

            var order = new Order
            {
                TableNumber = string.IsNullOrWhiteSpace(orderReq.TableNumber) ? "طلبية موبايل" : orderReq.TableNumber,
                CustomerName = "",
                Status = OrderStatus.Pending,
                OrderTime = DateTime.Now,
                Notes = orderReq.Notes ?? "",
                Discount = orderReq.Discount,
                CreatedBy = createdBy ?? "",
                Items = new List<OrderItem>()
            };

            var allItems = DatabaseHelper.GetAllMenuItems(availableOnly: true);

            foreach (var reqItem in orderReq.Items)
            {
                var menuItem = allItems.Find(m => m.ItemID == reqItem.ItemID);
                if (menuItem == null || reqItem.Quantity <= 0) continue;

                order.Items.Add(new OrderItem
                {
                    ItemID = menuItem.ItemID,
                    ItemName = menuItem.Name,
                    UnitPrice = menuItem.Price,
                    Quantity = reqItem.Quantity
                });
            }

            if (order.Items.Count == 0)
            {
                WriteJson(response, 400, new { success = false, message = "الأصناف المطلوبة مش موجودة أو مش متوفرة حالياً" });
                return;
            }

            int orderId = DatabaseHelper.PlaceOrder(order);

            WriteJson(response, 200, new { success = true, orderId = orderId });
        }

        private static void HandleGetOrders(HttpListenerRequest request, HttpListenerResponse response)
        {
            string status = request.QueryString["status"];
            if (string.IsNullOrWhiteSpace(status)) status = "All";

            DateTime? date = null;
            string dateStr = request.QueryString["date"];
            if (!string.IsNullOrWhiteSpace(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
                date = parsedDate;

            var orders = DatabaseHelper.GetOrders(status, date);

            var result = new List<object>();
            foreach (var o in orders)
            {
                result.Add(new
                {
                    orderId = o.OrderID,
                    tableNumber = o.TableNumber,
                    customerName = o.CustomerName,
                    createdBy = o.CreatedBy,
                    items = o.Items.Select(i => new { name = i.ItemName, quantity = i.Quantity }).ToList(),
                    total = o.GrandTotal,
                    status = o.Status.ToString(),
                    orderTime = o.OrderTime.ToString("dd MMM hh:mm tt")
                });
            }

            WriteJson(response, 200, new { success = true, orders = result });
        }

        private class AdvanceOrderRequest
        {
            public int OrderId { get; set; }
        }

        private static void HandleAdvanceOrder(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var req = JsonConvert.DeserializeObject<AdvanceOrderRequest>(body);
            if (req == null || req.OrderId <= 0)
            {
                WriteJson(response, 400, new { success = false, message = "رقم الطلبية مطلوب" });
                return;
            }

            var order = DatabaseHelper.GetOrderById(req.OrderId);
            if (order == null)
            {
                WriteJson(response, 404, new { success = false, message = "الطلبية غير موجودة" });
                return;
            }

            OrderStatus next;
            switch (order.Status)
            {
                case OrderStatus.Pending: next = OrderStatus.Preparing; break;
                case OrderStatus.Preparing: next = OrderStatus.Ready; break;
                case OrderStatus.Ready: next = OrderStatus.Completed; break;
                case OrderStatus.Cancelled: next = OrderStatus.Pending; break;
                default:
                    WriteJson(response, 400, new { success = false, message = "الطلبية مكتملة أصلاً" });
                    return;
            }

            DatabaseHelper.UpdateOrderStatus(req.OrderId, next);
            WriteJson(response, 200, new { success = true, newStatus = next.ToString() });
        }

        private class DeleteOrderRequest
        {
            public int OrderId { get; set; }
        }

        private static void HandleDeleteOrder(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var req = JsonConvert.DeserializeObject<DeleteOrderRequest>(body);
            if (req == null || req.OrderId <= 0)
            {
                WriteJson(response, 400, new { success = false, message = "رقم الطلبية مطلوب" });
                return;
            }

            var order = DatabaseHelper.GetOrderById(req.OrderId);
            if (order == null)
            {
                WriteJson(response, 404, new { success = false, message = "الطلبية غير موجودة" });
                return;
            }

            if (order.Status == OrderStatus.Preparing || order.Status == OrderStatus.Ready)
            {
                WriteJson(response, 400, new { success = false, message = "ما فيك تحذف طلب قيد التنفيذ. الغيه أول." });
                return;
            }

            DatabaseHelper.DeleteOrder(req.OrderId);
            WriteJson(response, 200, new { success = true });
        }

        private static void HandleDeliverAll(HttpListenerResponse response)
        {
            var pending = DatabaseHelper.GetOrders("Pending", null);
            var preparing = DatabaseHelper.GetOrders("Preparing", null);
            var ready = DatabaseHelper.GetOrders("Ready", null);
            var allActive = pending.Concat(preparing).Concat(ready).ToList();

            if (allActive.Count == 0)
            {
                WriteJson(response, 200, new { success = true, count = 0, total = 0 });
                return;
            }

            decimal total = 0;
            foreach (var order in allActive)
            {
                total += order.GrandTotal;
                DatabaseHelper.UpdateOrderStatus(order.OrderID, OrderStatus.Completed);
            }

            DatabaseHelper.AddToCashRegister(total, $"Deliver All - {DateTime.Now}");

            WriteJson(response, 200, new { success = true, count = allActive.Count, total = total });
        }

        private static void HandleNewDay(HttpListenerResponse response)
        {
            DatabaseHelper.ClearAllOrders();
            WriteJson(response, 200, new { success = true });
        }

        private static void HandleGetReports(HttpListenerRequest request, HttpListenerResponse response)
        {
            DateTime from = DateTime.Today.AddDays(-30);
            DateTime to = DateTime.Today;

            string fromStr = request.QueryString["from"];
            string toStr = request.QueryString["to"];
            if (!string.IsNullOrWhiteSpace(fromStr) && DateTime.TryParse(fromStr, out var parsedFrom)) from = parsedFrom.Date;
            if (!string.IsNullOrWhiteSpace(toStr) && DateTime.TryParse(toStr, out var parsedTo)) to = parsedTo.Date;

            if (from > to)
            {
                WriteJson(response, 400, new { success = false, message = "تاريخ البداية لازم يكون قبل تاريخ النهاية" });
                return;
            }

            var orders = DatabaseHelper.GetOrders("All", null);
            int total = 0; decimal revenue = 0; int completed = 0, cancelled = 0;
            foreach (var o in orders)
            {
                if (o.OrderTime.Date >= from && o.OrderTime.Date <= to)
                {
                    total++;
                    if (o.Status == OrderStatus.Completed) { revenue += o.GrandTotal; completed++; }
                    if (o.Status == OrderStatus.Cancelled) cancelled++;
                }
            }
            decimal avgOrder = completed > 0 ? revenue / completed : 0;

            var catData = DatabaseHelper.GetSalesByCategory(from, to);
            var categories = new List<object>();
            foreach (DataRow r in catData.Rows)
            {
                categories.Add(new
                {
                    category = r["Category"].ToString(),
                    totalQty = Convert.ToInt32(r["TotalQty"]),
                    revenue = Convert.ToDecimal(r["Revenue"])
                });
            }

            var topData = DatabaseHelper.GetTopSellingItems(from, to, 8);
            var topItems = new List<object>();
            foreach (DataRow r in topData.Rows)
            {
                topItems.Add(new
                {
                    itemName = r["ItemName"].ToString(),
                    totalSold = Convert.ToInt32(r["TotalSold"]),
                    revenue = Convert.ToDecimal(r["Revenue"])
                });
            }

            WriteJson(response, 200, new
            {
                success = true,
                totalOrders = total,
                revenue = revenue,
                avgOrder = avgOrder,
                completed = completed,
                cancelled = cancelled,
                salesByCategory = categories,
                topItems = topItems
            });
        }

        private static void HandleGetDebtCustomers(HttpListenerResponse response)
        {
            var customers = DatabaseHelper.GetAllDebtCustomers();
            var result = new List<object>();
            foreach (var name in customers)
            {
                var debts = DatabaseHelper.GetDebtsByCustomer(name);
                decimal total = debts.Sum(d => d.TotalAmount);
                decimal paid = debts.Sum(d => d.PaidAmount);
                decimal remaining = total - paid;
                result.Add(new { name, total, paid, remaining });
            }
            WriteJson(response, 200, new { success = true, customers = result });
        }

        private static void HandleGetCustomerDebts(HttpListenerRequest request, HttpListenerResponse response)
        {
            string name = request.QueryString["name"];
            if (string.IsNullOrWhiteSpace(name))
            {
                WriteJson(response, 400, new { success = false, message = "اسم الزبون مطلوب" });
                return;
            }

            var debts = DatabaseHelper.GetDebtsByCustomer(name);
            var result = debts.Select(d => new
            {
                id = d.Id,
                date = d.Date.ToString("dd/MM/yyyy"),
                items = d.Items.Select(i => new { name = i.ItemName, quantity = i.Quantity }).ToList(),
                total = d.TotalAmount,
                paid = d.PaidAmount,
                remaining = d.RemainingAmount,
                status = d.RemainingAmount <= 0 ? "Paid" : "Remaining"
            }).ToList();

            decimal totalRemaining = debts.Where(d => d.RemainingAmount > 0).Sum(d => d.RemainingAmount);

            WriteJson(response, 200, new { success = true, debts = result, totalRemaining });
        }

        private class AddCustomerRequest
        {
            public string Name { get; set; }
        }

        private static void HandleAddDebtCustomer(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var req = JsonConvert.DeserializeObject<AddCustomerRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
            {
                WriteJson(response, 400, new { success = false, message = "اسم الزبون مطلوب" });
                return;
            }

            string name = req.Name.Trim();
            var existing = DatabaseHelper.GetAllDebtCustomers();
            if (existing.Contains(name))
            {
                WriteJson(response, 400, new { success = false, message = "الزبون موجود أصلاً" });
                return;
            }

            var emptyOrder = new Order
            {
                TableNumber = "0",
                CustomerName = name,
                Items = new List<OrderItem>(),
                Discount = 0
            };
            DatabaseHelper.SaveDebt(emptyOrder, name);

            WriteJson(response, 200, new { success = true });
        }

        private class DeleteCustomerRequest
        {
            public string Name { get; set; }
        }

        private static void HandleDeleteDebtCustomer(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var req = JsonConvert.DeserializeObject<DeleteCustomerRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
            {
                WriteJson(response, 400, new { success = false, message = "اسم الزبون مطلوب" });
                return;
            }

            DatabaseHelper.DeleteCustomerDebts(req.Name.Trim());
            WriteJson(response, 200, new { success = true });
        }

        private class PayDebtRequest
        {
            public string CustomerName { get; set; }
            public decimal Amount { get; set; }
        }

        private static void HandlePayDebt(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            var req = JsonConvert.DeserializeObject<PayDebtRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.CustomerName) || req.Amount <= 0)
            {
                WriteJson(response, 400, new { success = false, message = "بيانات الدفع غير صحيحة" });
                return;
            }

            var unpaidDebts = DatabaseHelper.GetDebtsByCustomer(req.CustomerName)
                .Where(d => d.RemainingAmount > 0)
                .OrderBy(d => d.Date)
                .ToList();

            if (unpaidDebts.Count == 0)
            {
                WriteJson(response, 400, new { success = false, message = "لا يوجد دين متبقي لهذا الزبون" });
                return;
            }

            decimal totalRemaining = unpaidDebts.Sum(d => d.RemainingAmount);
            if (req.Amount > totalRemaining)
            {
                WriteJson(response, 400, new { success = false, message = $"المبلغ ({req.Amount:N0}) أكبر من الدين المتبقي ({totalRemaining:N0})" });
                return;
            }

            decimal remaining = req.Amount;
            foreach (var debt in unpaidDebts)
            {
                if (remaining <= 0) break;
                decimal payAmount = Math.Min(remaining, debt.RemainingAmount);
                DatabaseHelper.RecordDebtPayment(debt.Id, payAmount);
                remaining -= payAmount;
            }

            var fakeOrder = new Order
            {
                TableNumber = "DEBT",
                CustomerName = req.CustomerName,
                Notes = $"سداد دين - {DateTime.Now:dd/MM/yyyy HH:mm}",
                Items = new List<OrderItem> { new OrderItem { ItemName = "سداد دين", UnitPrice = req.Amount, Quantity = 1 } },
                Status = OrderStatus.Completed,
                Discount = 0
            };
            DatabaseHelper.PlaceOrder(fakeOrder);

            WriteJson(response, 200, new { success = true });
        }

        private static void WriteJson(HttpListenerResponse response, int statusCode, object data)
        {
            string json = JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.StatusCode = statusCode;
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
