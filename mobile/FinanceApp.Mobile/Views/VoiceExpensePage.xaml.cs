using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class VoiceExpensePage : ContentPage
{
    private readonly VoiceExpenseViewModel _vm;

    public VoiceExpensePage(VoiceExpenseViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Saved += OnSaved;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        _vm.Saved -= OnSaved;
        base.OnDisappearing();
    }

    private async void OnSaved() => await Shell.Current.GoToAsync("..");
}
