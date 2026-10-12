using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class CategoryEditPage : ContentPage
{
    private readonly CategoryEditViewModel _vm;

    public CategoryEditPage(CategoryEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Finished += async () =>
        {
            if (Navigation.NavigationStack.Count > 1) await Navigation.PopAsync();
        };
    }

    public void Initialize(CategoryDto? category) => _vm.Initialize(category);

    private void OnColorTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject b && b.BindingContext is string hex)
            _vm.ColorHex = hex;
    }
}
