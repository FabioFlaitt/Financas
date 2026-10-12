using System.ComponentModel;

namespace FinanceApp.Domain.Enums;

/// <summary>Natureza de um lançamento financeiro.</summary>
public enum TransactionType
{
    [Description("Receita")] Income = 1,
    [Description("Despesa")] Expense = 2,
    [Description("Transferência")] Transfer = 3,
    [Description("Estorno")] Refund = 4,
    /// <summary>Pagamento de fatura de cartão (não é despesa nova).</summary>
    [Description("Pagamento de fatura")] CreditCardPayment = 5
}

/// <summary>Situação de um lançamento ao longo do seu ciclo de vida.</summary>
public enum TransactionStatus
{
    [Description("Previsto")] Forecast = 1,
    [Description("Pendente")] Pending = 2,
    [Description("Confirmado")] Confirmed = 3,
    [Description("Cancelado")] Canceled = 4
}

/// <summary>Forma de pagamento utilizada no lançamento.</summary>
public enum PaymentMethod
{
    [Description("Dinheiro")] Cash = 1,
    [Description("Débito")] Debit = 2,
    [Description("Crédito")] Credit = 3,
    [Description("Pix")] Pix = 4,
    [Description("Boleto")] Boleto = 5,
    [Description("Transferência")] BankTransfer = 6,
    [Description("Outro")] Other = 99
}

/// <summary>Frequência de uma recorrência.</summary>
public enum RecurrenceFrequency
{
    [Description("Nenhuma")] None = 0,
    [Description("Diária")] Daily = 1,
    [Description("Semanal")] Weekly = 2,
    [Description("Quinzenal")] Biweekly = 3,
    [Description("Mensal")] Monthly = 4,
    [Description("Bimestral")] Bimonthly = 5,
    [Description("Trimestral")] Quarterly = 6,
    [Description("Semestral")] Semiannual = 7,
    [Description("Anual")] Yearly = 8
}

/// <summary>Como o lançamento entrou no sistema.</summary>
public enum TransactionOrigin
{
    [Description("Manual")] Manual = 1,
    [Description("Open Finance")] OpenFinance = 2,
    [Description("Notificação autorizada")] AuthorizedNotification = 3,
    [Description("Arquivo importado")] ImportedFile = 4
}

/// <summary>Tipo de conta bancária.</summary>
public enum AccountType
{
    [Description("Conta corrente")] Checking = 1,
    [Description("Poupança")] Savings = 2,
    [Description("Carteira")] Wallet = 3,
    [Description("Investimento")] Investment = 4,
    [Description("Outro")] Other = 99
}

/// <summary>Situação de uma fatura de cartão de crédito.</summary>
public enum InvoiceStatus
{
    [Description("Aberta")] Open = 1,
    [Description("Fechada")] Closed = 2,
    [Description("Paga")] Paid = 3,
    [Description("Vencida")] Overdue = 4
}

/// <summary>Situação de uma meta financeira.</summary>
public enum GoalStatus
{
    [Description("Em andamento")] InProgress = 1,
    [Description("Concluída")] Completed = 2,
    [Description("Cancelada")] Canceled = 3
}

/// <summary>Situação de uma conexão bancária (Open Finance).</summary>
public enum BankConnectionStatus
{
    [Description("Pendente")] Pending = 1,
    [Description("Conectada")] Connected = 2,
    [Description("Consentimento expirado")] Expired = 3,
    [Description("Acesso revogado")] Revoked = 4,
    [Description("Erro")] Error = 5,
    /// <summary>Conexão de demonstração com dados fictícios identificados.</summary>
    [Description("Demonstração")] Demo = 6
}

/// <summary>Resultado de uma tentativa de sincronização bancária.</summary>
public enum SyncStatus
{
    [Description("Sucesso")] Success = 1,
    [Description("Parcial")] Partial = 2,
    [Description("Falha")] Failed = 3
}

/// <summary>Classifica a despesa como fixa ou variável (para análises).</summary>
public enum ExpenseNature
{
    [Description("Não se aplica")] NotApplicable = 0,
    [Description("Fixa")] Fixed = 1,
    [Description("Variável")] Variable = 2
}

/// <summary>Situação de uma viagem de carro.</summary>
public enum TripStatus
{
    [Description("Em andamento")] InProgress = 1,
    [Description("Concluída")] Finished = 2,
    [Description("Cancelada")] Canceled = 3
}

/// <summary>Tipo de gasto registrado durante uma viagem.</summary>
public enum TripExpenseType
{
    [Description("Pedágio")] Toll = 1,
    [Description("Combustível")] Fuel = 2,
    [Description("Alimentação")] Food = 3,
    [Description("Estacionamento")] Parking = 4,
    [Description("Outro")] Other = 99
}

/// <summary>Tipos de notificação do próprio aplicativo.</summary>
public enum NotificationType
{
    [Description("Vencimento de conta")] BillDue = 1,
    [Description("Orçamento próximo do limite")] BudgetNearLimit = 2,
    [Description("Despesa acima do limite")] ExpenseOverLimit = 3,
    [Description("Falha na sincronização")] SyncFailure = 4,
    [Description("Vencimento de fatura")] InvoiceDue = 5,
    [Description("Resumo semanal")] WeeklySummary = 6,
    [Description("Resumo mensal")] MonthlySummary = 7
}
