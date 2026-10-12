using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class CardEditPage : ContentPage
{
    private readonly CardEditViewModel _vm;

    public CardEditPage(CardEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += OnFinished;
    }

    public async Task InitializeAsync(CardDto? card) => await _vm.InitializeAsync(card);

    private void OnColorTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is string hex)
            _vm.PickColorCommand.Execute(hex);
    }

    private async void OnFinished()
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
