using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _vm;

    public CategoriesPage(CategoriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCommand.ExecuteAsync(null);
    }

    private async void OnAddClicked(object? sender, EventArgs e) => await OpenEditorAsync(null);

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is CategoryDto c)
            await OpenEditorAsync(c);
    }

    private async Task OpenEditorAsync(CategoryDto? category)
    {
        var page = ServiceHelper.Get<CategoryEditPage>();
        page.Initialize(category);
        await Navigation.PushAsync(page);
    }
}
