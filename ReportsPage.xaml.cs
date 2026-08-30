using System.Collections.ObjectModel;
using BistroPOS.Mobile.Services;

namespace BistroPOS.Mobile;

public partial class ReportsPage : ContentPage
{
    private readonly ApiService _api = new();
    private readonly ObservableCollection<CategorySalesViewModel> _categories = new();
    private readonly ObservableCollection<TopItemViewModel> _topItems = new();

    public ReportsPage()
    {
        InitializeComponent();
        CategoryCollectionView.ItemsSource = _categories;
        TopItemsCollectionView.ItemsSource = _topItems;

        FromDatePicker.Date = DateTime.Today.AddDays(-30);
        ToDatePicker.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadReportAsync();
    }

    private void OnTodayClicked(object sender, EventArgs e)
    {
        FromDatePicker.Date = DateTime.Today;
        ToDatePicker.Date = DateTime.Today;
    }

    private void OnThisMonthClicked(object sender, EventArgs e)
    {
        FromDatePicker.Date = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        ToDatePicker.Date = DateTime.Today;
    }

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        await LoadReportAsync();
    }

    private async Task LoadReportAsync()
    {
        if (FromDatePicker.Date > ToDatePicker.Date)
        {
            await DisplayAlert("تنبيه", "تاريخ البداية لازم يكون قبل تاريخ النهاية", "حسناً");
            return;
        }

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;

        var result = await _api.GetReportsAsync(FromDatePicker.Date, ToDatePicker.Date);

        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;

        if (result == null || !result.Success)
        {
            await DisplayAlert("خطأ", "ما قدرنا نجيب التقرير من السيرفر", "حسناً");
            return;
        }

        BuildMetrics(result);

        _categories.Clear();
        foreach (var c in result.SalesByCategory)
            _categories.Add(new CategorySalesViewModel(c));

        _topItems.Clear();
        int rank = 1;
        foreach (var i in result.TopItems)
            _topItems.Add(new TopItemViewModel(i, rank++));
    }

    private void BuildMetrics(ReportsDto r)
    {
        MetricsStack.Children.Clear();

        var metrics = new (string title, string value, Color color)[]
        {
            ("إجمالي الطلبات", r.TotalOrders.ToString(), Color.FromArgb("#BA7517")),
            ("الإيرادات", $"{r.Revenue:N0} ل.ل", Color.FromArgb("#1E8C3C")),
            ("متوسط الطلبية", $"{r.AvgOrder:N0} ل.ل", Color.FromArgb("#3C78B4")),
            ("مكتملة", r.Completed.ToString(), Color.FromArgb("#1E8C64")),
            ("ملغاة", r.Cancelled.ToString(), Color.FromArgb("#B43C3C")),
        };

        foreach (var m in metrics)
        {
            var card = new Frame
            {
                WidthRequest = 130,
                Padding = 12,
                CornerRadius = 10,
                BorderColor = Color.FromArgb("#E0E0E0"),
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = m.value, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = m.color },
                        new Label { Text = m.title, FontSize = 12, TextColor = Colors.Gray }
                    }
                }
            };
            MetricsStack.Children.Add(card);
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}

public class CategorySalesViewModel
{
    public string Category { get; }
    public string QtyText { get; }
    public string RevenueText { get; }

    public CategorySalesViewModel(CategorySalesDto dto)
    {
        Category = dto.Category;
        QtyText = $"{dto.TotalQty} صنف";
        RevenueText = $"{dto.Revenue:N0} ل.ل";
    }
}

public class TopItemViewModel
{
    public string RankText { get; }
    public string ItemName { get; }
    public string QtyText { get; }
    public string RevenueText { get; }

    public TopItemViewModel(TopItemDto dto, int rank)
    {
        RankText = $"{rank}.";
        ItemName = dto.ItemName;
        QtyText = $"{dto.TotalSold} قطعة";
        RevenueText = $"{dto.Revenue:N0} ل.ل";
    }
}
