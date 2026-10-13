using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>
/// Fluxo da tela inicial: ou o usuário cria o perfil local (primeira abertura) ou apenas entra no app
/// (voltas seguintes). Não há senha nem servidor: o perfil só identifica os dados neste aparelho.
/// </summary>
public partial class WelcomeViewModel : ObservableObject
{
    private readonly LocalFinanceStore _store;

    public WelcomeViewModel(LocalFinanceStore store) => _store = store;

    [ObservableProperty] private bool _isReady;
    [ObservableProperty] private bool _isFirstTime = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _savedName = "você";
    [ObservableProperty] private string? _errorMessage;

    /// <summary>Descobre se já há um perfil para decidir qual modo mostrar.</summary>
    public async Task LoadAsync()
    {
        try
        {
            if (await _store.HasProfileAsync())
            {
                IsFirstTime = false;
                SavedName = (await _store.GetProfileNameAsync()) ?? "você";
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsReady = true; }
    }

    [RelayCommand]
    private async Task CreateProfileAsync()
    {
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Como você quer ser chamado? Informe seu nome.";
            return;
        }
        if (!string.IsNullOrWhiteSpace(Email) && !LooksLikeEmail(Email))
        {
            ErrorMessage = "E-mail inválido. Deixe em branco se preferir.";
            return;
        }
        IsBusy = true;
        try
        {
            await _store.CreateProfileAsync(Name, string.IsNullOrWhiteSpace(Email) ? null : Email);
            Finished?.Invoke();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Enter() => Finished?.Invoke();

    private static bool LooksLikeEmail(string value)
        => Regex.IsMatch(value.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    public event Action? Finished;
}
