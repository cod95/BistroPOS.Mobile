using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BistroPOS.Mobile.Services;

namespace BistroPOS.Mobile;

public partial class OrdersPage : ContentPage
{
    private readonly ObservableCollection<OrderViewModel> _orders = new();
    private readonly ApiService _api = new();
    private HashSet<int> _seenOrderIds = new();

    private readonly string[] _statusValues = { "All", "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
    private readonly string[] _statusLabels = { "الكل", "قيد الانتظار", "تحضير", "جاهزة", "مكتملة", "ملغاة" };

    public OrdersPage()
    {
        InitializeComponent();
        OrdersCollectionView.ItemsSource = _orders;

        StatusPicker.ItemsSource = _statusLabels;
        StatusPicker.SelectedIndex = 0;

        LoadSeenIds();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrdersAsync();
    }

    private void LoadSeenIds()
    {
        string raw = Preferences.Get("SeenOrderIds", "");
        _seenOrderIds = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s, out int v) ? v : -1)
                            .Where(v => v > 0)
                            .ToHashSet();
    }

    private void SaveSeenOrderId(int orderId)
    {
        _seenOrderIds.Add(orderId);
        Preferences.Set("SeenOrderIds", string.Join(",", _seenOrderIds));
    }

    private async void OnStatusFilterChanged(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        string status = _statusValues[Math.Max(StatusPicker.SelectedIndex, 0)];
        var orders = await _api.GetOrdersAsync(status);

        if (orders == null)
        {
            await DisplayAlert("خطأ", "ما قدرنا نجيب لائحة الطلبات من السيرفر", "حسناً");
            return;
        }

        _orders.Clear();
        foreach (var o in orders)
            _orders.Add(new OrderViewModel(o, isNew: !_seenOrderIds.Contains(o.OrderId)));
    }

    private void OnOrderCardTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is OrderViewModel vm)
        {
            vm.MarkSeen();
            SaveSeenOrderId(vm.OrderId);
        }
    }

    private async void OnActionClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not OrderViewModel vm) return;

        var result = await _api.AdvanceOrderAsync(vm.OrderId);
        if (result != null && result.Success)
        {
            await LoadOrdersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بتحديث الطلبية", "حسناً");
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not OrderViewModel vm) return;

        bool confirm = await DisplayAlert("تأكيد", $"حذف الطلبية {vm.OrderIdDisplay}؟", "نعم", "لأ");
        if (!confirm) return;

        var result = await _api.DeleteOrderAsync(vm.OrderId);
        if (result != null && result.Success)
        {
            await LoadOrdersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بحذف الطلبية", "حسناً");
        }
    }

    private async void OnDeliverAllClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("تأكيد", "تسليم كل الطلبات النشطة؟", "نعم", "لأ");
        if (!confirm) return;

        var result = await _api.DeliverAllAsync();
        if (result != null && result.Success)
        {
            await DisplayAlert("تم", $"تم تسليم {result.Count} طلب، الإجمالي: {result.Total:N0} ل.ل", "تمام");
            await LoadOrdersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", "صار خطأ بتسليم الطلبات", "حسناً");
        }
    }

    private async void OnNewDayClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("يوم جديد", "سيتم حذف كل الطلبات النشطة، متأكد؟", "نعم", "لأ");
        if (!confirm) return;

        bool success = await _api.NewDayAsync();
        if (success)
        {
            await DisplayAlert("تم", "تم بدء يوم جديد وحذف كل الطلبات", "تمام");
            await LoadOrdersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", "صار خطأ بتنفيذ يوم جديد", "حسناً");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}

public class OrderViewModel : INotifyPropertyChanged
{
    public int OrderId { get; }
    public string OrderIdDisplay { get; }
    public string ItemsText { get; }
    public string TotalText { get; }
    public string StatusText { get; }
    public Color StatusColor { get; }
    public string OrderTime { get; }
    public string? ActionText { get; }
    public bool HasAction { get; }
    public bool CanDelete { get; }

    private bool _isNew;
    public bool IsNew
    {
        get => _isNew;
        private set { _isNew = value; OnChanged(nameof(IsNew)); OnChanged(nameof(CardBorderColor)); }
    }

    public Color CardBorderColor => IsNew ? Color.FromArgb("#E67E22") : Color.FromArgb("#E0E0E0");

    public OrderViewModel(OrderDto dto, bool isNew)
    {
        OrderId = dto.OrderId;
        OrderIdDisplay = $"#{dto.OrderId:D4}";
        ItemsText = string.Join("\n", dto.Items.Select(i => $"{i.Name} {i.Quantity}"));
        TotalText = $"{dto.Total:N0} ل.ل";
        OrderTime = dto.OrderTime;
        _isNew = isNew;

        switch (dto.Status)
        {
            case "Pending":
                StatusText = "قيد الانتظار"; StatusColor = Color.FromArgb("#B46400");
                ActionText = "بدء التحضير"; HasAction = true; CanDelete = true;
                break;
            case "Preparing":
                StatusText = "تحضير"; StatusColor = Color.FromArgb("#1E64B4");
                ActionText = "جاهز"; HasAction = true; CanDelete = false;
                break;
            case "Ready":
                StatusText = "جاهزة"; StatusColor = Color.FromArgb("#1E8C3C");
                ActionText = "تسليم"; HasAction = true; CanDelete = false;
                break;
            case "Completed":
                StatusText = "مكتملة"; StatusColor = Colors.Gray;
                ActionText = null; HasAction = false; CanDelete = true;
                break;
            case "Cancelled":
                StatusText = "ملغاة"; StatusColor = Color.FromArgb("#A01E1E");
                ActionText = "استرجاع"; HasAction = true; CanDelete = true;
                break;
            default:
                StatusText = dto.Status; StatusColor = Colors.Black;
                ActionText = null; HasAction = false; CanDelete = true;
                break;
        }
    }

    public void MarkSeen() => IsNew = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
