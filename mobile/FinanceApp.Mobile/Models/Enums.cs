namespace FinanceApp.Mobile.Models;

// Espelham os enums do backend (serializados como texto no JSON).

public enum TransactionType { Income = 1, Expense = 2, Transfer = 3, Refund = 4, CreditCardPayment = 5 }
public enum TransactionStatus { Forecast = 1, Pending = 2, Confirmed = 3, Canceled = 4 }
public enum PaymentMethod { Cash = 1, Debit = 2, Credit = 3, Pix = 4, Boleto = 5, BankTransfer = 6, Other = 99 }
public enum RecurrenceFrequency { None = 0, Daily = 1, Weekly = 2, Biweekly = 3, Monthly = 4, Bimonthly = 5, Quarterly = 6, Semiannual = 7, Yearly = 8 }
public enum ExpenseNature { NotApplicable = 0, Fixed = 1, Variable = 2 }
public enum AccountType { Checking = 1, Savings = 2, Wallet = 3, Investment = 4, Other = 99 }
public enum TripStatus { InProgress = 1, Finished = 2, Canceled = 3 }
public enum TripExpenseType { Toll = 1, Fuel = 2, Food = 3, Parking = 4, Other = 99 }

public static class EnumLabels
{
    public static string Label(this TransactionType t) => t switch
    {
        TransactionType.Income => "Receita",
        TransactionType.Expense => "Despesa",
        TransactionType.Transfer => "Transferência",
        TransactionType.Refund => "Estorno",
        TransactionType.CreditCardPayment => "Pagamento de fatura",
        _ => t.ToString()
    };

    public static string Label(this RecurrenceFrequency f) => f switch
    {
        RecurrenceFrequency.None => "Nenhuma",
        RecurrenceFrequency.Daily => "Diária",
        RecurrenceFrequency.Weekly => "Semanal",
        RecurrenceFrequency.Biweekly => "Quinzenal",
        RecurrenceFrequency.Monthly => "Mensal",
        RecurrenceFrequency.Bimonthly => "Bimestral",
        RecurrenceFrequency.Quarterly => "Trimestral",
        RecurrenceFrequency.Semiannual => "Semestral",
        RecurrenceFrequency.Yearly => "Anual",
        _ => f.ToString()
    };

    public static string Label(this TripExpenseType t) => t switch
    {
        TripExpenseType.Toll => "Pedágio",
        TripExpenseType.Fuel => "Combustível",
        TripExpenseType.Food => "Alimentação",
        TripExpenseType.Parking => "Estacionamento",
        TripExpenseType.Other => "Outro",
        _ => t.ToString()
    };

    public static string Label(this AccountType t) => t switch
    {
        AccountType.Checking => "Conta corrente",
        AccountType.Savings => "Poupança",
        AccountType.Wallet => "Carteira",
        AccountType.Investment => "Investimento",
        _ => "Outro"
    };
}
