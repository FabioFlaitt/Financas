using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class BudgetEditPage : ContentPage
{
    private readonly BudgetEditViewModel _vm;

    public BudgetEditPage(BudgetEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += OnFinished;
    }

    /// <summary>Carrega o editor; passe o orçamento para editar ou null (com o período) para criar.</summary>
    public async Task InitializeAsync(BudgetItem? item, int year, int month) => await _vm.InitializeAsync(item, year, month);

    private async void OnFinished()
    {
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
