using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinanceApp.Mobile.Converters;
using FinanceApp.Mobile.Models;
using FinanceApp.Mobile.Services;
using Microsoft.Maui.Storage;

namespace FinanceApp.Mobile.ViewModels;

/// <summary>Uma linha da prévia de importação, com seleção para incluir/excluir.</summary>
public partial class ImportRow : ObservableObject
{
    public ImportRow(ImportItem item)
    {
        Item = item;
        IsSelected = !item.IsDuplicate; // duplicados vêm desmarcados por padrão
    }

    public ImportItem Item { get; }
    [ObservableProperty] private bool _isSelected;

    public string Description => Item.Description;
    public bool IsDuplicate => Item.IsDuplicate;
    public string DateLabel => Item.Date.ToString("dd/MM/yyyy", BrazilCulture.PtBr);
    public string AmountLabel => Item.Amount.ToString("C2", BrazilCulture.PtBr);
    public string AmountColorHex => Item.Type == TransactionType.Expense ? "#C62828" : "#2E7D32";
}

public partial class ImportViewModel : ObservableObject
{
    private readonly FinanceApiClient _api;
    private string? _content;
    private string _format = "Csv";

    public ImportViewModel(FinanceApiClient api) => _api = api;

    public ObservableCollection<PickerOption<Guid?>> Accounts { get; } = new();
    [ObservableProperty] private PickerOption<Guid?>? _selectedAccount;

    public ObservableCollection<ImportRow> Rows { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasFile;
    [ObservableProperty] private bool _hasPreview;
    [ObservableProperty] private string _fileName = "Nenhum arquivo selecionado";
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private string? _errorMessage;

    public async Task InitializeAsync()
    {
        if (Accounts.Count > 0) return;
        try
        {
            var accts = await _api.ListAccountsAsync();
            foreach (var a in accts) Accounts.Add(new PickerOption<Guid?>(a.Id, a.Name));
            SelectedAccount = Accounts.FirstOrDefault();
        }
        catch { /* sem contas carregadas */ }
    }

    [RelayCommand]
    private async Task PickFileAsync()
    {
        ErrorMessage = null;
        ResultMessage = null;
        try
        {
            var file = await FilePicker.Default.PickAsync();
            if (file is null) return;

            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            _content = await reader.ReadToEndAsync();

            FileName = file.FileName;
            _format = file.FileName.EndsWith(".ofx", StringComparison.OrdinalIgnoreCase) ? "Ofx" : "Csv";
            HasFile = true;
            HasPreview = false;
            Rows.Clear();
            StatusMessage = $"Arquivo {_format.ToUpperInvariant()} pronto para análise.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível ler o arquivo. " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        ResultMessage = null;
        if (SelectedAccount?.Value is not Guid accountId) { ErrorMessage = "Selecione a conta."; return; }
        if (string.IsNullOrWhiteSpace(_content)) { ErrorMessage = "Escolha um arquivo primeiro."; return; }

        IsBusy = true;
        try
        {
            var preview = await _api.PreviewImportAsync(new { accountId, format = _format, content = _content });
            Rows.Clear();
            foreach (var item in preview.Items) Rows.Add(new ImportRow(item));
            HasPreview = Rows.Count > 0;
            StatusMessage = $"{preview.TotalParsed} lançamentos encontrados · {preview.Duplicates} já importados.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Falha ao analisar. " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;
        if (SelectedAccount?.Value is not Guid accountId) { ErrorMessage = "Selecione a conta."; return; }

        var items = Rows.Where(r => r.IsSelected).Select(r => r.Item).ToList();
        if (items.Count == 0) { ErrorMessage = "Selecione ao menos um lançamento."; return; }

        IsBusy = true;
        try
        {
            var result = await _api.CommitImportAsync(new { accountId, items });
            ResultMessage = $"Importados: {result.Imported} · Ignorados (duplicados): {result.SkippedDuplicates}.";
            Rows.Clear();
            HasPreview = false;
            HasFile = false;
            _content = null;
            FileName = "Nenhum arquivo selecionado";
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Falha ao importar. " + ex.Message;
        }
        finally { IsBusy = false; }
    }
}
