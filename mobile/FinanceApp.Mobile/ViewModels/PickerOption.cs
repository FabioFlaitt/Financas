namespace FinanceApp.Mobile.ViewModels;

/// <summary>Opção genérica para Pickers (valor + rótulo exibido).</summary>
public record PickerOption<T>(T Value, string Label)
{
    public override string ToString() => Label; // Picker usa ToString por padrão
}
