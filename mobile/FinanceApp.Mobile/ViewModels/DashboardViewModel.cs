using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Drawables;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private readonly SessionService _session;

    public DashboardViewModel(FinanceApiClient api, SessionService session)
    {
        _api = api;
        _session = session;
    }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private string _greeting = "Olá!";

    [ObservableProperty] private DashboardResult? _dashboard;
    [ObservableProperty] private IDrawable? _categoryChart;
    [ObservableProperty] private IDrawable? _balanceChart;

    public IReadOnlyList<CategorySlice> Categories => Dashboard?.ExpensesByCategory ?? new List<CategorySlice>();
    public IReadOnlyList<TopExpenseItem> TopExpenses => Dashboard?.TopExpenses ?? new List<TopExpenseItem>();

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            await _session.LoadAsync();

            // Tenta autenticar com o usuário de demonstração quando ainda não há sessão.
            if (!_session.IsAuthenticated)
            {
                try
                {
                    var auth = await _api.LoginAsync("demo@financas.app", "Demo@12345");
                    await _session.SignInAsync(auth.AccessToken, auth.Name);
                }
                catch { /* sem backend/credenciais: cai no fallback de demonstração abaixo */ }
            }

            DashboardResult data;
            if (_session.IsAuthenticated)
            {
                data = await _api.GetDashboardAsync();
                IsDemo = false;
            }
            else if (ApiConfig.FallbackToDemoData)
            {
                data = DemoDashboardData.Build();
                IsDemo = true;
            }
            else
            {
                throw new ApiException("Não foi possível conectar ao servidor.");
            }

            Apply(data);
            Greeting = _session.UserName is { Length: > 0 } name ? $"Olá, {name.Split(' ')[0]}!" : "Olá!";
        }
        catch (Exception ex) when (ApiConfig.FallbackToDemoData)
        {
            // Qualquer falha de rede: exibe dados de demonstração claramente identificados.
            Apply(DemoDashboardData.Build());
            IsDemo = true;
            _ = ex; // silenciado propositalmente no fallback
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = "Não foi possível carregar o painel. Verifique sua conexão e tente novamente.";
            _ = ex;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Apply(DashboardResult data)
    {
        Dashboard = data;

        var date = new DateTime(data.Year, data.Month, 1);
        var label = date.ToString("MMMM 'de' yyyy", BrazilCulture.PtBr);
        MonthLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(label);

        var hole = Color.FromArgb("#FFFFFF");
        CategoryChart = new DonutChartDrawable(data.ExpensesByCategory, hole);
        BalanceChart = new LineChartDrawable(data.DailyBalanceEvolution, Color.FromArgb("#0D47A1"));

        OnPropertyChanged(nameof(Categories));
        OnPropertyChanged(nameof(TopExpenses));
    }
}
