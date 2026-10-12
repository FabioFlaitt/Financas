using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class InstallmentEditPage : ContentPage
{
    private readonly InstallmentEditViewModel _vm;

    public InstallmentEditPage(InstallmentEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += OnFinished;
    }

    public async Task InitializeAsync(Guid cardId, string cardName) => await _vm.InitializeAsync(cardId, cardName);

    private async void OnFinished()
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
