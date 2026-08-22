using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BistroPOS.Mobile.Services;

namespace BistroPOS.Mobile;

public partial class OrderPage : ContentPage
{
    private readonly ObservableCollection<MenuItemViewModel> _menuItems = new();
    private readonly ApiService _api = new();

    public OrderPage()
    {
        InitializeComponent();
        MenuCollectionView.ItemsSource = _menuItems;
        DiscountEntry.TextChanged += (s, e) => UpdateTotal();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        var items = await _api.GetMenuAsync();

        if (items == null)
        {
            await DisplayAlert("خطأ", "تعذّر جلب قائمة الأصناف من السيرفر", "حسناً");
            return;
        }

        _menuItems.Clear();
        foreach (var item in items)
        {
            _menuItems.Add(new MenuItemViewModel
            {
                ItemId = item.ItemId,
                Name = item.Name,
                Category = item.Category,
                Price = item.Price,
                Quantity = 0
            });
        }
        UpdateTotal();
    }

    private void OnIncreaseClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is MenuItemViewModel vm)
        {
            vm.Quantity++;
            UpdateTotal();
        }
    }

    private void OnDecreaseClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is MenuItemViewModel vm)
        {
            if (vm.Quantity > 0) vm.Quantity--;
            UpdateTotal();
        }
    }

    private void OnDeferredToggled(object sender, ToggledEventArgs e)
    {
        TableEntry.IsVisible = e.Value;
        if (!e.Value)
            TableEntry.Text = string.Empty;
    }

    private void UpdateTotal()
    {
        decimal subtotal = _menuItems.Sum(i => i.Subtotal);
        decimal.TryParse(DiscountEntry.Text, out decimal discount);
        decimal total = subtotal - discount;
        if (total < 0) total = 0;
        TotalLabel.Text = $"الإجمالي: {total:N0} ل.ل";
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnSubmitOrderClicked(object sender, EventArgs e)
    {
        var selectedItems = _menuItems.Where(i => i.Quantity > 0).ToList();

        if (selectedItems.Count == 0)
        {
            await DisplayAlert("تنبيه", "يجب اختيار صنف واحد عالأقل", "حسناً");
            return;
        }

        if (DeferredSwitch.IsToggled && string.IsNullOrWhiteSpace(TableEntry.Text))
        {
            await DisplayAlert("تنبيه", "اكتب اسم الزبون أو رقم الطاولة للفاتورة المؤجلة", "حسناً");
            return;
        }

        decimal.TryParse(DiscountEntry.Text, out decimal discount);

        var request = new CreateOrderRequest
        {
            TableNumber = DeferredSwitch.IsToggled ? TableEntry.Text.Trim() : "طلبية فرعية",
            Discount = discount,
            Items = selectedItems.Select(i => new OrderItemRequest
            {
                ItemID = i.ItemId,
                Quantity = i.Quantity
            }).ToList()
        };

        var result = await _api.CreateOrderAsync(request);

        if (result != null && result.Success)
        {
            await DisplayAlert("تم", $"الطلبية أُرسلت بنجاح، رقم الطلبية: {result.OrderId}", "حسنًا");
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "خطأ بإرسال الطلبية", "حاول مجدداً");
        }
    }
}

public class MenuItemViewModel : INotifyPropertyChanged
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }

    private int _quantity;
    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnChanged(nameof(Quantity)); OnChanged(nameof(Subtotal)); }
    }

    public decimal Subtotal => Price * Quantity;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
