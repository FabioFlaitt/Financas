using System.Globalization;
using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Converters;

public static class BrazilCulture
{
    public static readonly CultureInfo PtBr = new("pt-BR");
}

/// <summary>decimal -> "R$ 1.234,56" (pt-BR).</summary>
public class CurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = System.Convert.ToDecimal(value ?? 0m);
        return amount.ToString("C2", BrazilCulture.PtBr);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>decimal -> "+R$ 1.000,00" / "-R$ 500,00" com sinal explícito.</summary>
public class SignedCurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = System.Convert.ToDecimal(value ?? 0m);
        var sign = amount > 0 ? "+" : string.Empty;
        return sign + amount.ToString("C2", BrazilCulture.PtBr);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>decimal -> verde (>= 0) ou vermelho (&lt; 0). Cor é complemento do texto, nunca o único sinal.</summary>
public class AmountColorConverter : IValueConverter
{
    private static readonly Color Positive = Color.FromArgb("#2E7D32");
    private static readonly Color Negative = Color.FromArgb("#C62828");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = System.Convert.ToDecimal(value ?? 0m);
        return amount >= 0 ? Positive : Negative;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>decimal -> "51,6%".</summary>
public class PercentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return "—";
        var pct = System.Convert.ToDecimal(value);
        return pct.ToString("0.0", BrazilCulture.PtBr) + "%";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);
}

public class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>TransactionDto -> valor com sinal ("+R$ 100,00" / "-R$ 50,00") conforme o tipo.</summary>
public class TransactionAmountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TransactionDto t) return string.Empty;
        var sign = t.Type switch
        {
            TransactionType.Income or TransactionType.Refund => "+",
            TransactionType.Expense or TransactionType.CreditCardPayment => "-",
            _ => string.Empty
        };
        return sign + t.Amount.ToString("C2", BrazilCulture.PtBr);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>TransactionType -> cor (receita verde, despesa vermelha, demais neutro).</summary>
public class TransactionTypeColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is TransactionType t
            ? t switch
            {
                TransactionType.Income or TransactionType.Refund => Color.FromArgb("#2E7D32"),
                TransactionType.Expense or TransactionType.CreditCardPayment => Color.FromArgb("#C62828"),
                _ => Color.FromArgb("#607D8B")
            }
            : Colors.Gray;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>string hex ("#RRGGBB") -> Color, para colorir elementos a partir dos dados.</summary>
public class HexColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string;
        if (string.IsNullOrWhiteSpace(hex)) return Colors.Gray;
        try { return Color.FromArgb(hex); }
        catch { return Colors.Gray; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
