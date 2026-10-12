using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class CardInvoicesPage : ContentPage
{
    private readonly CardInvoicesViewModel _vm;
    private CardDto? _card;

    public CardInvoicesPage(CardInvoicesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void SetCard(CardDto card)
    {
        _card = card;
        _vm.SetCard(card);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_card is null) return;
        var page = ServiceHelper.Get<CardEditPage>();
        await page.InitializeAsync(_card);
        await Navigation.PushAsync(page);
    }

    private async void OnInstallmentClicked(object? sender, EventArgs e)
    {
        if (_card is null) return;
        var page = ServiceHelper.Get<InstallmentEditPage>();
        await page.InitializeAsync(_card.Id, _card.Name);
        await Navigation.PushAsync(page);
    }

    private async void OnInvoiceTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is InvoiceListItem invoice)
        {
            var page = ServiceHelper.Get<InvoiceDetailPage>();
            await page.InitializeAsync(invoice.Id);
            await Navigation.PushAsync(page);
        }
    }
}
