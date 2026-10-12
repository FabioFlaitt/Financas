using FinanceApp.Domain.Common;

namespace FinanceApp.Domain.Entities;

/// <summary>
/// Cartão de crédito do usuário, com dia de fechamento/vencimento e limites.
/// </summary>
public class CreditCard : BaseEntity, IUserOwnedEntity
{
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Apelido opcional (ex.: "Cartão da viagem").</summary>
    public string? Nickname { get; set; }

    /// <summary>Bandeira/instituição (ex.: "Visa Nubank").</summary>
    public string? Brand { get; set; }

    /// <summary>Limite total do cartão, quando informado.</summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>Dia do mês em que a fatura fecha (1-31).</summary>
    public int ClosingDay { get; set; }

    /// <summary>Dia do mês de vencimento da fatura (1-31).</summary>
    public int DueDay { get; set; }

    public string ColorHex { get; set; } = "#6A1B9A";
    public string Icon { get; set; } = "credit-card";

    public bool IsArchived { get; set; }

    /// <summary>Conta usada por padrão para pagar a fatura.</summary>
    public Guid? DefaultPaymentAccountId { get; set; }

    public ICollection<CreditCardInvoice> Invoices { get; set; } = new List<CreditCardInvoice>();
}
