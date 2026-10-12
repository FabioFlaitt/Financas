using FinanceApp.Application.Categories;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceApp.Infrastructure.Persistence;

/// <summary>
/// Semeia um usuário de DEMONSTRAÇÃO com dados fictícios (não reais) para que o dashboard e os
/// gráficos funcionem imediatamente. Idempotente: só executa se o usuário demo ainda não existir.
/// NUNCA usar dados financeiros reais em ambiente de desenvolvimento.
/// </summary>
public class DemoDataSeeder
{
    public const string DemoEmail = "demo@financas.app";
    public const string DemoPassword = "Demo@12345";

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(AppDbContext db, IPasswordHasher hasher, ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == DemoEmail, ct))
            return;

        _logger.LogInformation("Semeando dados de demonstração (usuário {Email}).", DemoEmail);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var prevStart = monthStart.AddMonths(-1);

        var user = new User
        {
            Name = "Usuário Demonstração",
            Email = DemoEmail,
            PasswordHash = _hasher.Hash(DemoPassword)
        };
        _db.Users.Add(user);

        // Categorias padrão
        var categories = DefaultCategories.CreateFor(user.Id).ToList();
        _db.Categories.AddRange(categories);
        Guid Cat(string name) => categories.First(c => c.Name == name).Id;

        // Preferências de notificação
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
            _db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Type = type });

        // Contas
        var checking = new Account { UserId = user.Id, Name = "Conta Corrente", Institution = "Banco Demo", Type = AccountType.Checking, InitialBalance = 2000m, ColorHex = "#1565C0", Icon = "bank" };
        var savings = new Account { UserId = user.Id, Name = "Poupança", Institution = "Banco Demo", Type = AccountType.Savings, InitialBalance = 5000m, ColorHex = "#00897B", Icon = "piggy-bank" };
        var wallet = new Account { UserId = user.Id, Name = "Carteira", Type = AccountType.Wallet, InitialBalance = 150m, ColorHex = "#F9A825", Icon = "wallet" };
        _db.Accounts.AddRange(checking, savings, wallet);

        // Cartão de crédito + fatura do mês
        var card = new CreditCard
        {
            UserId = user.Id,
            Name = "Nubank",
            Brand = "Mastercard",
            CreditLimit = 5000m,
            ClosingDay = 3,
            DueDay = 10,
            ColorHex = "#6A1B9A",
            DefaultPaymentAccountId = checking.Id
        };
        _db.CreditCards.Add(card);

        var invoice = new CreditCardInvoice
        {
            UserId = user.Id,
            CreditCardId = card.Id,
            ReferenceMonth = monthStart,
            ClosingDate = new DateOnly(today.Year, today.Month, Math.Min(3, DateTime.DaysInMonth(today.Year, today.Month))),
            DueDate = new DateOnly(today.Year, today.Month, Math.Min(10, DateTime.DaysInMonth(today.Year, today.Month))),
            Status = InvoiceStatus.Open
        };
        _db.CreditCardInvoices.Add(invoice);

        var transactions = new List<Transaction>();

        void Cash(string desc, decimal amount, TransactionType type, string? category, Account account,
                  int day, TransactionStatus status = TransactionStatus.Confirmed, ExpenseNature nature = ExpenseNature.Variable,
                  DateOnly? baseMonth = null)
        {
            var month = baseMonth ?? monthStart;
            var d = new DateOnly(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
            transactions.Add(new Transaction
            {
                UserId = user.Id,
                Description = desc,
                Amount = amount,
                Type = type,
                Status = status,
                TransactionDate = d,
                CategoryId = category is null ? null : Cat(category),
                AccountId = account.Id,
                Nature = nature,
                PaymentMethod = type == TransactionType.Income ? PaymentMethod.BankTransfer : PaymentMethod.Debit
            });
        }

        void CardPurchase(string desc, decimal amount, string category, int day, string? merchant = null)
        {
            var d = new DateOnly(today.Year, today.Month, Math.Min(day, DateTime.DaysInMonth(today.Year, today.Month)));
            transactions.Add(new Transaction
            {
                UserId = user.Id,
                Description = desc,
                Amount = amount,
                Type = TransactionType.Expense,
                Status = TransactionStatus.Confirmed,
                TransactionDate = d,
                CategoryId = Cat(category),
                CreditCardId = card.Id,
                InvoiceId = invoice.Id,
                Merchant = merchant,
                PaymentMethod = PaymentMethod.Credit,
                Nature = ExpenseNature.Variable
            });
        }

        // --- Mês atual ---
        Cash("Salário", 7500m, TransactionType.Income, "Outros", checking, 5);
        Cash("Aluguel", 2200m, TransactionType.Expense, "Moradia", checking, 10, nature: ExpenseNature.Fixed);
        Cash("Supermercado Pão de Açúcar", 450m, TransactionType.Expense, "Alimentação e supermercado", checking, 3);
        Cash("Supermercado Carrefour", 320m, TransactionType.Expense, "Alimentação e supermercado", checking, 11);
        Cash("Farmácia", 90m, TransactionType.Expense, "Saúde e farmácia", checking, 8);

        CardPurchase("iFood", 85m, "Restaurantes e delivery", 6, "iFood");
        CardPurchase("Restaurante Outback", 120m, "Restaurantes e delivery", 9, "Outback");
        CardPurchase("Posto Shell", 250m, "Transporte e combustível", 7, "Shell");
        CardPurchase("Netflix", 55m, "Assinaturas e serviços digitais", 2, "Netflix");

        // Previstos (futuro) para o fluxo projetado
        Cash("Conta de energia", 180m, TransactionType.Expense, "Contas de consumo", checking, 20, TransactionStatus.Forecast, ExpenseNature.Fixed);
        Cash("Internet", 120m, TransactionType.Expense, "Assinaturas e serviços digitais", checking, 15, TransactionStatus.Forecast, ExpenseNature.Fixed);

        // Transferência entre contas próprias (não conta como despesa)
        var transfer = new Transaction
        {
            UserId = user.Id,
            Description = "Reserva na poupança",
            Amount = 1000m,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Confirmed,
            TransactionDate = new DateOnly(today.Year, today.Month, Math.Min(5, DateTime.DaysInMonth(today.Year, today.Month))),
            AccountId = checking.Id,
            DestinationAccountId = savings.Id,
            ExcludeFromReports = true,
            PaymentMethod = PaymentMethod.Pix
        };
        transactions.Add(transfer);

        // --- Mês anterior (para comparação) ---
        Cash("Salário", 7500m, TransactionType.Income, "Outros", checking, 5, baseMonth: prevStart);
        Cash("Aluguel", 2200m, TransactionType.Expense, "Moradia", checking, 10, nature: ExpenseNature.Fixed, baseMonth: prevStart);
        Cash("Supermercado", 700m, TransactionType.Expense, "Alimentação e supermercado", checking, 12, baseMonth: prevStart);
        Cash("Restaurantes", 400m, TransactionType.Expense, "Restaurantes e delivery", checking, 15, baseMonth: prevStart);
        Cash("Combustível", 300m, TransactionType.Expense, "Transporte e combustível", checking, 18, baseMonth: prevStart);

        _db.Transactions.AddRange(transactions);

        // Atualiza o total da fatura com as compras do mês
        invoice.TotalAmount = transactions
            .Where(t => t.CreditCardId == card.Id && t.InvoiceId == invoice.Id)
            .Sum(t => t.Amount);

        // Orçamentos do mês
        _db.Budgets.AddRange(
            new Budget { UserId = user.Id, Year = today.Year, Month = today.Month, CategoryId = null, LimitAmount = 6000m },
            new Budget { UserId = user.Id, Year = today.Year, Month = today.Month, CategoryId = Cat("Alimentação e supermercado"), LimitAmount = 1000m },
            new Budget { UserId = user.Id, Year = today.Year, Month = today.Month, CategoryId = Cat("Restaurantes e delivery"), LimitAmount = 500m },
            new Budget { UserId = user.Id, Year = today.Year, Month = today.Month, CategoryId = Cat("Transporte e combustível"), LimitAmount = 400m });

        // Meta financeira
        _db.Goals.Add(new FinancialGoal
        {
            UserId = user.Id,
            Name = "Reserva de emergência",
            TargetAmount = 20000m,
            CurrentAmount = 5000m,
            TargetDate = monthStart.AddMonths(12),
            LinkedAccountId = savings.Id,
            ColorHex = "#00897B",
            Icon = "shield"
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Dados de demonstração semeados com sucesso.");
    }
}
