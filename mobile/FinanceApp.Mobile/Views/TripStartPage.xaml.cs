using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;

namespace FinanceApp.Mobile.Views;

public partial class TripStartPage : ContentPage
{
    private readonly TripStartViewModel _vm;

    public TripStartPage(TripStartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Started += OnStarted;
    }

    private async void OnStarted(Guid tripId)
    {
        // Abre o detalhe da viagem recém-criada e remove o formulário da pilha.
        var detail = ServiceHelper.Get<TripDetailPage>();
        await detail.InitializeAsync(tripId);

        var nav = Navigation;
        await nav.PushAsync(detail);
        if (nav.NavigationStack.Count > 2)
        {
            var self = nav.NavigationStack[nav.NavigationStack.Count - 2];
            nav.RemovePage(self);
        }
    }
}
