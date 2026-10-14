using FinanceApp.Mobile.Services;
using FinanceApp.Mobile.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace FinanceApp.Mobile.Views;

public partial class TripStartPage : ContentPage
{
    private readonly TripStartViewModel _vm;
    private Polyline? _preview;
    private bool _centeredOnce;

    public TripStartPage(TripStartViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.Started += OnStarted;
        _vm.RoutePreview += OnRoutePreview;
        _vm.CenterMap += OnCenterMap;
        PlanMap.MapClicked += OnMapClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync();
    }

    private async void OnMapClicked(object? sender, MapClickedEventArgs e)
        => await _vm.SetDestinationFromMapAsync(e.Location);

    private void OnCenterMap(Location loc)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var radius = _centeredOnce ? Distance.FromKilometers(5) : Distance.FromKilometers(3);
            PlanMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, radius));
            _centeredOnce = true;
        });
    }

    private void OnRoutePreview(PlannedRoute plan)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_preview is not null) PlanMap.MapElements.Remove(_preview);
            PlanMap.Pins.Clear();

            if (plan.Path.Count > 1)
            {
                _preview = new Polyline { StrokeColor = Color.FromArgb("#0D47A1"), StrokeWidth = 5 };
                foreach (var p in plan.Path) _preview.Geopath.Add(p);
                PlanMap.MapElements.Add(_preview);
                PlanMap.MoveToRegion(MapSpan.FromCenterAndRadius(plan.Path[plan.Path.Count / 2], Distance.FromKilometers(20)));

                var dest = plan.Path[plan.Path.Count - 1];
                PlanMap.Pins.Add(new Pin { Label = "Destino", Location = dest, Type = PinType.Place });
            }

            foreach (var toll in plan.Tolls)
                PlanMap.Pins.Add(new Pin { Label = toll.Name, Location = new Location(toll.Latitude, toll.Longitude), Type = PinType.Generic });
        });
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
