using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class InvoiceDetailPage : ContentPage
{
    private readonly InvoiceDetailViewModel _vm;

    public InvoiceDetailPage(InvoiceDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public async Task InitializeAsync(Guid invoiceId) => await _vm.InitializeAsync(invoiceId);
}
