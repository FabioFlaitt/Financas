using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>
/// Fluxo "ditar o gasto": o usuário segura o botão de microfone, fala uma frase curta e o app monta
/// um rascunho do lançamento (valor, categoria, data, forma de pagamento), que ele confirma com um toque.
/// Nenhuma rede: tanto o reconhecimento de voz quanto o parser rodam no aparelho.
/// </summary>
public partial class VoiceExpenseViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private readonly ISpeechToTextService _speech;

    public VoiceExpenseViewModel(FinanceApiClient api, ISpeechToTextService speech)
    {
        _api = api;
        _speech = speech;
    }

    public ObservableCollection<PickerOption<Guid>> Categories { get; } = new();
    public ObservableCollection<PickerOption<Guid>> Accounts { get; } = new();
    public ObservableCollection<PickerOption<PaymentMethod?>> PaymentMethods { get; } = new()
    {
        new(null, "Sem forma de pagamento"),
        new(PaymentMethod.Credit, "Crédito"),
        new(PaymentMethod.Debit, "Débito"),
        new(PaymentMethod.Pix, "Pix"),
        new(PaymentMethod.Cash, "Dinheiro"),
        new(PaymentMethod.Boleto, "Boleto"),
        new(PaymentMethod.BankTransfer, "Transferência"),
    };

    [ObservableProperty] private bool _isListening;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private string _hint = "Toque no microfone e fale, por exemplo: \"gastei 50 reais no mercado ontem no cartão\".";
    [ObservableProperty] private string? _transcript;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private TransactionType _type = TransactionType.Expense;
    [ObservableProperty] private PickerOption<Guid>? _selectedCategory;
    [ObservableProperty] private PickerOption<Guid>? _selectedAccount;
    [ObservableProperty] private PickerOption<PaymentMethod?>? _selectedPaymentMethod;

    public bool IsIncome
    {
        get => Type == TransactionType.Income;
        set { Type = value ? TransactionType.Income : TransactionType.Expense; OnPropertyChanged(); }
    }

    /// <summary>Carrega as listas que a tela de confirmação usa (categorias/contas do usuário).</summary>
    public async Task InitializeAsync()
    {
        if (Categories.Count > 0) return;
        try
        {
            foreach (var c in await _api.ListCategoriesAsync())
                Categories.Add(new PickerOption<Guid>(c.Id, c.Name));
            foreach (var a in await _api.ListAccountsAsync())
                Accounts.Add(new PickerOption<Guid>(a.Id, a.Name));
            SelectedAccount = Accounts.FirstOrDefault();
            SelectedPaymentMethod = PaymentMethods[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar categorias/contas: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task ListenAsync()
    {
        ErrorMessage = null; SuccessMessage = null;
        if (IsListening) { await _speech.CancelAsync(); IsListening = false; return; }

        try
        {
            if (!await _speech.RequestPermissionAsync())
            {
                ErrorMessage = "Preciso de permissão para usar o microfone e o reconhecimento de voz.";
                return;
            }

            IsListening = true;
            Transcript = null;
            Hint = "Ouvindo... fale o que você gastou.";

            var text = await _speech.ListenAsync(new CultureInfo("pt-BR"), partial => Transcript = partial);
            Transcript = text;
            if (string.IsNullOrWhiteSpace(text))
            {
                ErrorMessage = "Não entendi nada. Pode tentar de novo?";
                return;
            }
            ApplyDraft(VoiceExpenseParser.Parse(text, DateOnly.FromDateTime(DateTime.Today)));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não consegui gravar o áudio: " + ex.Message;
        }
        finally
        {
            IsListening = false;
            Hint = "Toque no microfone e fale, por exemplo: \"gastei 50 reais no mercado ontem no cartão\".";
        }
    }

    private void ApplyDraft(ParsedVoiceExpense draft)
    {
        Description = draft.Description;
        AmountText = draft.Amount is decimal a ? a.ToString("0.00", BrazilCulture.PtBr) : string.Empty;
        Date = draft.TransactionDate.ToDateTime(TimeOnly.MinValue);
        Type = draft.Type;
        OnPropertyChanged(nameof(IsIncome));

        SelectedCategory = draft.CategoryHint is { Length: > 0 } hint
            ? FindCategory(hint) ?? SelectedCategory
            : SelectedCategory;
        SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.Value == draft.PaymentMethod) ?? PaymentMethods[0];
        HasDraft = true;
    }

    private PickerOption<Guid>? FindCategory(string hint)
    {
        var normalized = Normalize(hint);
        return Categories.FirstOrDefault(c => Normalize(c.Label).Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (!TryParseAmount(AmountText, out var amount) || amount <= 0)
        {
            ErrorMessage = "Informe um valor maior que zero.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Description))
        {
            ErrorMessage = "Informe uma descrição.";
            return;
        }

        IsBusy = true;
        try
        {
            await _api.CreateTransactionAsync(new
            {
                description = Description.Trim(),
                amount,
                type = Type,
                status = TransactionStatus.Confirmed,
                transactionDate = DateOnly.FromDateTime(Date),
                categoryId = SelectedCategory?.Value,
                accountId = SelectedAccount?.Value,
                paymentMethod = SelectedPaymentMethod?.Value,
                notes = Transcript is { Length: > 0 } t ? $"Ditado por voz: \"{t}\"" : null
            });
            SuccessMessage = "Lançamento adicionado.";
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível salvar: " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    private static bool TryParseAmount(string text, out decimal value)
    {
        var normalized = (text ?? string.Empty).Replace(" ", string.Empty).Replace(",", ".");
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    public event Action? Saved;
}
