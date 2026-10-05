using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BarcodeScanning;
using BistroPOS.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace BistroPOS.Mobile;

public partial class OrderPage : ContentPage
{
    private readonly ObservableCollection<MenuItemViewModel> _menuItems = new();
    private List<MenuItemViewModel> _allMenuItems = new();
    private readonly ApiService _api = new();
    private string _selectedCategory = "الكل";

    public OrderPage()
    {
        InitializeComponent();
        MenuCollectionView.ItemsSource = _menuItems;
        DiscountEntry.TextChanged += (s, e) => UpdateTotal();
        SearchEntry.TextChanged += (s, e) => ApplyFilters();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMenuAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (ScannerCameraView != null)
            ScannerCameraView.CameraEnabled = false;
    }

    private async Task LoadMenuAsync()
    {
        var items = await _api.GetMenuAsync();

        if (items == null)
        {
            await DisplayAlert("خطأ", "ما قدرنا نجيب المينيو من السيرفر", "حسناً");
            return;
        }

        _allMenuItems = items.Select(item => new MenuItemViewModel
        {
            ItemId = item.ItemId,
            Name = item.Name,
            Category = item.Category,
            Price = item.Price,
            Barcode = item.Barcode,
            Quantity = 0
        }).ToList();

        BuildCategoryChips();
        ApplyFilters();
        UpdateTotal();
    }

    private void BuildCategoryChips()
    {
        CategoryFlex.Children.Clear();

        var categories = new List<string> { "الكل" };
        categories.AddRange(_allMenuItems.Select(i => i.Category).Distinct().OrderBy(c => c));

        foreach (var category in categories)
        {
            bool selected = category == _selectedCategory;

            var card = new Border
            {
                WidthRequest = 76,
                HeightRequest = 76,
                Margin = new Thickness(4),
                Padding = 4,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Stroke = selected ? Color.FromArgb("#FF6B00") : Color.FromArgb("#E0E0E0"),
                StrokeThickness = selected ? 2 : 1,
                BackgroundColor = selected ? Color.FromArgb("#FFF1E6") : Colors.White,
                Content = new VerticalStackLayout
                {
                    VerticalOptions = LayoutOptions.Center,
                    Spacing = 2,
                    Children =
                    {
                        new Label
                        {
                            Text = GetCategoryIcon(category),
                            FontSize = 24,
                            HorizontalOptions = LayoutOptions.Center
                        },
                        new Label
                        {
                            Text = category,
                            FontSize = 11,
                            FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None,
                            TextColor = selected ? Color.FromArgb("#FF6B00") : Color.FromArgb("#333333"),
                            HorizontalOptions = LayoutOptions.Center,
                            HorizontalTextAlignment = TextAlignment.Center,
                            LineBreakMode = LineBreakMode.TailTruncation,
                            MaxLines = 2
                        }
                    }
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, e) =>
            {
                _selectedCategory = category;
                BuildCategoryChips();
                ApplyFilters();
            };
            card.GestureRecognizers.Add(tap);

            CategoryFlex.Children.Add(card);
        }
    }

    private string GetCategoryIcon(string category)
    {
        if (category == "الكل") return "⭐";
        if (category.Contains("قهوة") || category.Contains("كوفي") || category.Contains("اسبريسو") || category.Contains("كابتشينو")) return "☕";
        if (category.Contains("مياه")) return "💧";
        if (category.Contains("عصير")) return "🧃";
        if (category.Contains("بارد")) return "🥤";
        if (category.Contains("ساخن")) return "☕";
        if (category.Contains("مناقيش") || category.Contains("منقوشة")) return "🫓";
        if (category.Contains("كرواسون")) return "🥐";
        if (category.Contains("حلو")) return "🍰";
        if (category.Contains("ساندويش") || category.Contains("سندويش")) return "🥪";
        if (category.Contains("بيتزا")) return "🍕";
        if (category.Contains("فطور") || category.Contains("افطار")) return "🍳";
        if (category.Contains("سلطة")) return "🥗";
        return "🍽️";
    }

    private void ApplyFilters()
    {
        string search = SearchEntry.Text?.Trim() ?? "";

        var filtered = _allMenuItems.Where(item =>
            (_selectedCategory == "الكل" || item.Category == _selectedCategory) &&
            (string.IsNullOrEmpty(search) || item.Name.Contains(search, System.StringComparison.OrdinalIgnoreCase))
        );

        _menuItems.Clear();
        foreach (var item in filtered)
            _menuItems.Add(item);
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
        decimal subtotal = _allMenuItems.Sum(i => i.Subtotal);
        CurrencyService.TryParse(DiscountEntry.Text, out decimal discount);
        decimal total = subtotal - discount;
        if (total < 0) total = 0;
        TotalLabel.Text = $"الإجمالي: {CurrencyService.Format(total)}";
    }

    private async void OnScanBarcodeClicked(object sender, EventArgs e)
    {
        await Methods.AskForRequiredPermissionAsync();
        ScannerOverlay.IsVisible = true;
        ScannerCameraView.CameraEnabled = true;
    }

    private void OnCloseScannerClicked(object sender, EventArgs e)
    {
        ScannerCameraView.CameraEnabled = false;
        ScannerOverlay.IsVisible = false;
    }

    private async void CameraView_OnDetectionFinished(object sender, OnDetectionFinishedEventArg e)
    {
        if (e.BarcodeResults.Count == 0) return;

        string code = e.BarcodeResults.First().RawValue;

        ScannerCameraView.CameraEnabled = false;
        ScannerOverlay.IsVisible = false;

        var match = _allMenuItems.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Barcode) && i.Barcode == code);
        if (match != null)
        {
            match.Quantity++;
            UpdateTotal();
            await DisplayAlert("تمت الإضافة", $"تمت إضافة \"{match.Name}\"", "تمام");
        }
        else
        {
            await DisplayAlert("غير موجود", "لم يتم العثور على صنف بهذا الباركود", "حسناً");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }

    private async void OnSubmitOrderClicked(object sender, EventArgs e)
    {
        var selectedItems = _allMenuItems.Where(i => i.Quantity > 0).ToList();

        if (selectedItems.Count == 0)
        {
            await DisplayAlert("تنبيه", "لازم تختار صنف واحد عالأقل", "حسناً");
            return;
        }

        if (DeferredSwitch.IsToggled && string.IsNullOrWhiteSpace(TableEntry.Text))
        {
            await DisplayAlert("تنبيه", "اكتب اسم الزبون أو رقم الطاولة للفاتورة المؤجلة", "حسناً");
            return;
        }

        CurrencyService.TryParse(DiscountEntry.Text, out decimal discount);

        var request = new CreateOrderRequest
        {
            TableNumber = DeferredSwitch.IsToggled ? TableEntry.Text.Trim() : "طلبية موبايل",
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
            await DisplayAlert("تم", $"الطلبية أُرسلت بنجاح، رقم الطلبية: {result.OrderId}", "تمام");
            await Shell.Current.GoToAsync("//MainPage");
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بإرسال الطلبية", "حاول مجدداً");
        }
    }
}

public class MenuItemViewModel : INotifyPropertyChanged
{
    public int ItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Barcode { get; set; } = string.Empty;

    public string PriceText => CurrencyService.Format(Price);

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
