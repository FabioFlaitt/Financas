using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>Lista de cartões com fatura atual e limite disponível.</summary>
public partial class CardsViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    public CardsViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<CardDto> Items { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _api.ListCardsAsync();
            Items.Clear();
            foreach (var c in list) Items.Add(c);
            HasItems = Items.Count > 0;
        }
        catch { HasItems = false; }
        finally { IsBusy = false; }
    }
}

/// <summary>Criação/edição de um cartão.</summary>
public partial class CardEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid? _id;
    public CardEditViewModel(FinanceApiClient api) => _api = api;

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _title = "Novo cartão";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _brand;
    [ObservableProperty] private string _limitText = string.Empty;
    [ObservableProperty] private double _closingDay = 1;
    [ObservableProperty] private double _dueDay = 10;
    [ObservableProperty] private string _colorHex = "#6A1B9A";
    [ObservableProperty] private string? _errorMessage;

    public string ClosingDayLabel => $"Fecha no dia {ClosingDay:0}";
    public string DueDayLabel => $"Vence no dia {DueDay:0}";
    partial void OnClosingDayChanged(double value) => OnPropertyChanged(nameof(ClosingDayLabel));
    partial void OnDueDayChanged(double value) => OnPropertyChanged(nameof(DueDayLabel));

    public ObservableCollection<string> Palette { get; } = new()
    {
        "#6A1B9A", "#5E35B1", "#1565C0", "#00838F", "#2E7D32", "#EF6C00",
        "#C62828", "#AD1457", "#283593", "#455A64", "#00695C", "#757575"
    };

    public ObservableCollection<PickerOption<Guid?>> Accounts { get; } = new();
    [ObservableProperty] private PickerOption<Guid?>? _selectedAccount;

    public async Task InitializeAsync(CardDto? card)
    {
        Accounts.Clear();
        Accounts.Add(new PickerOption<Guid?>(null, "Nenhuma"));
        try
        {
            var accts = await _api.ListAccountsAsync();
            foreach (var a in accts) Accounts.Add(new PickerOption<Guid?>(a.Id, a.Name));
        }
        catch { /* mantém ao menos a opção "Nenhuma" */ }

        if (card is null)
        {
            SelectedAccount = Accounts[0];
            return;
        }

        _id = card.Id;
        IsEditing = true;
        Title = "Editar cartão";
        Name = card.Name;
        Brand = card.Brand;
        LimitText = card.CreditLimit is decimal l ? l.ToString("0.00", BrazilCulture.PtBr) : string.Empty;
        ClosingDay = card.ClosingDay;
        DueDay = card.DueDay;
        ColorHex = card.ColorHex;
        SelectedAccount = Accounts.FirstOrDefault(o => o.Value == card.DefaultPaymentAccountId) ?? Accounts[0];
    }

    [RelayCommand]
    private void PickColor(string hex) => ColorHex = hex;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Name)) { ErrorMessage = "Informe o nome do cartão."; return; }

        decimal? limit = null;
        if (!string.IsNullOrWhiteSpace(LimitText))
        {
            if (!MoneyParser.TryParse(LimitText, out var parsed)) { ErrorMessage = "Limite inválido."; return; }
            limit = parsed;
        }

        var body = new
        {
            name = Name.Trim(),
            brand = Brand,
            creditLimit = limit,
            closingDay = (int)Math.Round(ClosingDay),
            dueDay = (int)Math.Round(DueDay),
            colorHex = ColorHex,
            icon = "credit-card",
            defaultPaymentAccountId = SelectedAccount?.Value
        };
        IsBusy = true;
        try
        {
            if (_id is Guid id) await _api.UpdateCardAsync(id, body);
            else await _api.CreateCardAsync(body);
            Finished?.Invoke();
        }
        catch (Exception ex) { ErrorMessage = "Não foi possível salvar. " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_id is not Guid id || IsBusy) return;
        IsBusy = true;
        try { await _api.DeleteCardAsync(id); Finished?.Invoke(); }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}

/// <summary>Faturas de um cartão (hub do cartão): resumo + histórico de faturas.</summary>
public partial class CardInvoicesViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    public CardInvoicesViewModel(FinanceApiClient api) => _api = api;

    public Guid CardId { get; private set; }
    public string CardName { get; private set; } = string.Empty;

    public ObservableCollection<InvoiceListItem> Items { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasItems;
    [ObservableProperty] private string _title = "Faturas";

    public void SetCard(CardDto card)
    {
        CardId = card.Id;
        CardName = card.Name;
        Title = card.Name;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy || CardId == Guid.Empty) return;
        IsBusy = true;
        try
        {
            var list = await _api.ListCardInvoicesAsync(CardId);
            Items.Clear();
            foreach (var i in list) Items.Add(i);
            HasItems = Items.Count > 0;
        }
        catch { HasItems = false; }
        finally { IsBusy = false; }
    }
}

/// <summary>Detalhe de uma fatura: compras, fechamento e pagamento.</summary>
public partial class InvoiceDetailViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid _invoiceId;
    public InvoiceDetailViewModel(FinanceApiClient api) => _api = api;

    public event Action? Changed;

    public ObservableCollection<InvoiceTransaction> Transactions { get; } = new();
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _title = "Fatura";
    [ObservableProperty] private string _periodLabel = string.Empty;
    [ObservableProperty] private string _totalLabel = "R$ 0,00";
    [ObservableProperty] private string _paidLabel = "R$ 0,00";
    [ObservableProperty] private string _outstandingLabel = "R$ 0,00";
    [ObservableProperty] private string _statusLabel = string.Empty;
    [ObservableProperty] private string _statusColorHex = "#1565C0";
    [ObservableProperty] private string _dueLabel = string.Empty;
    [ObservableProperty] private bool _canPay;
    [ObservableProperty] private string _payAmountText = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public ObservableCollection<PickerOption<Guid?>> Accounts { get; } = new();
    [ObservableProperty] private PickerOption<Guid?>? _selectedAccount;

    public async Task InitializeAsync(Guid invoiceId)
    {
        _invoiceId = invoiceId;

        Accounts.Clear();
        try
        {
            var accts = await _api.ListAccountsAsync();
            foreach (var a in accts) Accounts.Add(new PickerOption<Guid?>(a.Id, a.Name));
            SelectedAccount = Accounts.FirstOrDefault();
        }
        catch { /* sem contas carregadas */ }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var inv = await _api.GetInvoiceAsync(_invoiceId);
            Apply(inv);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }

    private void Apply(InvoiceDetail inv)
    {
        Title = inv.CardName;
        PeriodLabel = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            new DateTime(inv.ReferenceMonth.Year, inv.ReferenceMonth.Month, 1).ToString("MMMM 'de' yyyy", BrazilCulture.PtBr));
        TotalLabel = inv.TotalAmount.ToString("C2", BrazilCulture.PtBr);
        PaidLabel = inv.PaidAmount.ToString("C2", BrazilCulture.PtBr);
        OutstandingLabel = inv.OutstandingAmount.ToString("C2", BrazilCulture.PtBr);
        StatusLabel = inv.StatusLabel;
        StatusColorHex = inv.StatusColorHex;
        DueLabel = "Vence em " + inv.DueDate.ToString("dd/MM/yyyy", BrazilCulture.PtBr);
        CanPay = inv.OutstandingAmount > 0;
        PayAmountText = inv.OutstandingAmount > 0 ? inv.OutstandingAmount.ToString("0.00", BrazilCulture.PtBr) : string.Empty;

        Transactions.Clear();
        foreach (var t in inv.Transactions) Transactions.Add(t);
    }

    [RelayCommand]
    private async Task PayAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (SelectedAccount?.Value is not Guid accountId) { ErrorMessage = "Selecione a conta de pagamento."; return; }
        if (!MoneyParser.TryParse(PayAmountText, out var amount) || amount <= 0) { ErrorMessage = "Valor inválido."; return; }

        IsBusy = true;
        try
        {
            await _api.PayInvoiceAsync(_invoiceId, new { accountId, amount });
            Changed?.Invoke();
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = "Não foi possível pagar. " + ex.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _api.CloseInvoiceAsync(_invoiceId);
            Changed?.Invoke();
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsBusy = false; }
    }
}

/// <summary>Registro de uma compra parcelada no cartão.</summary>
public partial class InstallmentEditViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private Guid _cardId;
    public InstallmentEditViewModel(FinanceApiClient api) => _api = api;

    public event Action? Finished;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _cardName = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _totalText = string.Empty;
    [ObservableProperty] private double _installments = 2;
    [ObservableProperty] private DateTime _purchaseDate = DateTime.Today;
    [ObservableProperty] private string? _errorMessage;

    public string InstallmentsLabel => $"{Installments:0}x";
    public string PerInstallmentLabel
    {
        get
        {
            if (MoneyParser.TryParse(TotalText, out var total) && Installments >= 1)
                return (total / (decimal)Installments).ToString("C2", BrazilCulture.PtBr) + " por mês";
            return string.Empty;
        }
    }
    partial void OnInstallmentsChanged(double value) { OnPropertyChanged(nameof(InstallmentsLabel)); OnPropertyChanged(nameof(PerInstallmentLabel)); }
    partial void OnTotalTextChanged(string value) => OnPropertyChanged(nameof(PerInstallmentLabel));

    public ObservableCollection<PickerOption<Guid?>> Categories { get; } = new();
    [ObservableProperty] private PickerOption<Guid?>? _selectedCategory;

    public async Task InitializeAsync(Guid cardId, string cardName)
    {
        _cardId = cardId;
        CardName = cardName;

        Categories.Clear();
        Categories.Add(new PickerOption<Guid?>(null, "Sem categoria"));
        try
        {
            var cats = await _api.ListCategoriesAsync();
            foreach (var c in cats) Categories.Add(new PickerOption<Guid?>(c.Id, c.Name));
        }
        catch { /* mantém "Sem categoria" */ }
        SelectedCategory = Categories[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(Description)) { ErrorMessage = "Informe a descrição."; return; }
        if (!MoneyParser.TryParse(TotalText, out var total) || total <= 0) { ErrorMessage = "Valor total inválido."; return; }

        var body = new
        {
            description = Description.Trim(),
            totalAmount = total,
            numberOfInstallments = (int)Math.Round(Installments),
            purchaseDate = DateOnly.FromDateTime(PurchaseDate),
            categoryId = SelectedCategory?.Value
        };
        IsBusy = true;
        try
        {
            await _api.AddInstallmentAsync(_cardId, body);
            Finished?.Invoke();
        }
        catch (Exception ex) { ErrorMessage = "Não foi possível salvar. " + ex.Message; }
        finally { IsBusy = false; }
    }
}
