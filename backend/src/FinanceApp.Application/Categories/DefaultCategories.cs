using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;

namespace FinanceApp.Application.Categories;

/// <summary>
/// Catálogo das categorias iniciais sugeridas pelo produto. Usado ao criar um novo usuário
/// para semear categorias com cores e ícones. O usuário pode editar, excluir ou criar novas.
/// </summary>
public static class DefaultCategories
{
    public record Seed(string Name, string ColorHex, string Icon, ExpenseNature Nature);

    public static readonly IReadOnlyList<Seed> All = new List<Seed>
    {
        new("Moradia",                       "#5E35B1", "home",         ExpenseNature.Fixed),
        new("Alimentação e supermercado",    "#2E7D32", "cart",         ExpenseNature.Variable),
        new("Restaurantes e delivery",       "#EF6C00", "silverware",   ExpenseNature.Variable),
        new("Transporte e combustível",      "#1565C0", "car",          ExpenseNature.Variable),
        new("Saúde e farmácia",              "#C62828", "medical-bag",  ExpenseNature.Variable),
        new("Lazer e entretenimento",        "#AD1457", "gamepad",      ExpenseNature.Variable),
        new("Compras pessoais",              "#6A1B9A", "shopping",     ExpenseNature.Variable),
        new("Assinaturas e serviços digitais","#00838F", "cloud",       ExpenseNature.Fixed),
        new("Educação",                      "#283593", "school",       ExpenseNature.Fixed),
        new("Contas de consumo",             "#00695C", "flash",        ExpenseNature.Fixed),
        new("Impostos e tarifas",            "#455A64", "bank",         ExpenseNature.Fixed),
        new("Investimentos",                 "#1B5E20", "trending-up",  ExpenseNature.NotApplicable),
        new("Transferências",                "#546E7A", "swap",         ExpenseNature.NotApplicable),
        new("Outros",                        "#757575", "tag",          ExpenseNature.Variable),
    };

    public static IEnumerable<Category> CreateFor(Guid userId) =>
        All.Select(s => new Category
        {
            UserId = userId,
            Name = s.Name,
            ColorHex = s.ColorHex,
            Icon = s.Icon,
            DefaultNature = s.Nature,
            IsSystem = true
        });
}
