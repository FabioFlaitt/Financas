using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class CardsPage : ContentPage
{
    private readonly CardsViewModel _vm;

    public CardsPage(CardsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var page = ServiceHelper.Get<CardEditPage>();
        await page.InitializeAsync(null);
        await Navigation.PushAsync(page);
    }

    private async void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is CardDto card)
        {
            var page = ServiceHelper.Get<CardInvoicesPage>();
            page.SetCard(card);
            await Navigation.PushAsync(page);
        }
    }
}
