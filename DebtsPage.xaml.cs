using System.Collections.ObjectModel;
using System.Linq;
using BistroPOS.Mobile.Services;

namespace BistroPOS.Mobile;

public partial class DebtsPage : ContentPage
{
    private readonly ApiService _api = new();
    private readonly ObservableCollection<DebtCustomerViewModel> _customers = new();
    private readonly ObservableCollection<CustomerDebtViewModel> _debts = new();
    private string? _selectedCustomer;

    public DebtsPage()
    {
        InitializeComponent();
        CustomersCollectionView.ItemsSource = _customers;
        DebtsCollectionView.ItemsSource = _debts;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ShowCustomerList();
        await LoadCustomersAsync();
    }

    private void ShowCustomerList()
    {
        _selectedCustomer = null;
        CustomerListSection.IsVisible = true;
        CustomerDetailSection.IsVisible = false;
    }

    private async Task LoadCustomersAsync()
    {
        var customers = await _api.GetDebtCustomersAsync();
        if (customers == null)
        {
            await DisplayAlert("خطأ", "ما قدرنا نجيب لائحة الزبائن", "حسناً");
            return;
        }

        _customers.Clear();
        foreach (var c in customers)
            _customers.Add(new DebtCustomerViewModel(c));
    }

    private async void OnAddCustomerClicked(object sender, EventArgs e)
    {
        string name = NewCustomerEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            await DisplayAlert("تنبيه", "اكتب اسم الزبون", "حسناً");
            return;
        }

        var result = await _api.AddDebtCustomerAsync(name);
        if (result != null && result.Success)
        {
            NewCustomerEntry.Text = "";
            await LoadCustomersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بإضافة الزبون", "حسناً");
        }
    }

    private async void OnDeleteCustomerClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.BindingContext is not DebtCustomerViewModel vm) return;

        bool confirm = await DisplayAlert("تأكيد", $"حذف الزبون \"{vm.Name}\" وكل ديونه؟", "نعم", "لأ");
        if (!confirm) return;

        var result = await _api.DeleteDebtCustomerAsync(vm.Name);
        if (result != null && result.Success)
        {
            await LoadCustomersAsync();
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بحذف الزبون", "حسناً");
        }
    }

    private async void OnCustomerTapped(object sender, EventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not DebtCustomerViewModel vm) return;

        _selectedCustomer = vm.Name;
        SelectedCustomerLabel.Text = vm.Name;

        CustomerListSection.IsVisible = false;
        CustomerDetailSection.IsVisible = true;

        await LoadCustomerDebtsAsync();
    }

    private async Task LoadCustomerDebtsAsync()
    {
        if (string.IsNullOrEmpty(_selectedCustomer)) return;

        var result = await _api.GetCustomerDebtsAsync(_selectedCustomer);
        if (result == null || !result.Success)
        {
            await DisplayAlert("خطأ", "ما قدرنا نجيب ديون الزبون", "حسناً");
            return;
        }

        TotalRemainingLabel.Text = $"المتبقي الكلي: {result.TotalRemaining:N0} ل.ل";

        _debts.Clear();
        foreach (var d in result.Debts)
            _debts.Add(new CustomerDebtViewModel(d));
    }

    private void OnBackToListClicked(object sender, EventArgs e)
    {
        ShowCustomerList();
    }

    private async void OnPayClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedCustomer)) return;

        if (!decimal.TryParse(PaymentAmountEntry.Text, out decimal amount) || amount <= 0)
        {
            await DisplayAlert("تنبيه", "اكتب مبلغ دفعة صحيح", "حسناً");
            return;
        }

        bool confirm = await DisplayAlert("تأكيد", $"تسجيل دفعة {amount:N0} ل.ل لـ\"{_selectedCustomer}\"؟", "نعم", "لأ");
        if (!confirm) return;

        var result = await _api.PayDebtAsync(_selectedCustomer, amount);
        if (result != null && result.Success)
        {
            PaymentAmountEntry.Text = "";
            await DisplayAlert("تم", "تم تسجيل الدفعة بنجاح", "تمام");
            await LoadCustomerDebtsAsync();
        }
        else
        {
            await DisplayAlert("خطأ", result?.Message ?? "صار خطأ بتسجيل الدفعة", "حسناً");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//MainPage");
    }
}

public class DebtCustomerViewModel
{
    public string Name { get; }
    public string RemainingText { get; }
    public Color RemainingColor { get; }

    public DebtCustomerViewModel(DebtCustomerDto dto)
    {
        Name = dto.Name;
        RemainingText = dto.Remaining > 0 ? $"متبقي: {dto.Remaining:N0} ل.ل" : "مسدد بالكامل";
        RemainingColor = dto.Remaining > 0 ? Color.FromArgb("#A01E1E") : Color.FromArgb("#1E8C3C");
    }
}

public class CustomerDebtViewModel
{
    public string Date { get; }
    public string ItemsText { get; }
    public string AmountsText { get; }
    public string StatusText { get; }
    public Color StatusColor { get; }

    public CustomerDebtViewModel(CustomerDebtDto dto)
    {
        Date = dto.Date;
        ItemsText = dto.Items.Count > 0
            ? string.Join(", ", dto.Items.Select(i => $"{i.Name} {i.Quantity}"))
            : "بدون أصناف";
        AmountsText = $"الإجمالي: {dto.Total:N0} — المدفوع: {dto.Paid:N0} — المتبقي: {dto.Remaining:N0} ل.ل";
        StatusText = dto.Status == "Paid" ? "مسدد" : "متبقي";
        StatusColor = dto.Status == "Paid" ? Color.FromArgb("#1E8C3C") : Color.FromArgb("#A01E1E");
    }
}
