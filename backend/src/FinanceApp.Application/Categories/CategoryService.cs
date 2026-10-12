using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Common;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Categories;

public record CategoryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#607D8B";
    public string Icon { get; init; } = "tag";
    public ExpenseNature DefaultNature { get; init; }
    public string NatureLabel { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public bool IsArchived { get; init; }
    public Guid? ParentCategoryId { get; init; }
}

public record SaveCategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#607D8B";
    public string Icon { get; init; } = "tag";
    public ExpenseNature DefaultNature { get; init; } = ExpenseNature.Variable;
    public Guid? ParentCategoryId { get; init; }
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class CategoryService : ICategoryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public CategoryService(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private Guid UserId => _currentUser.UserId ?? throw new InvalidOperationException("Usuário não autenticado.");

    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CancellationToken ct = default)
    {
        var list = await _db.Categories
            .Where(c => c.UserId == UserId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return list.Select(Map).ToList();
    }

    public async Task<CategoryDto> CreateAsync(SaveCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException(nameof(request.Name), "O nome da categoria é obrigatório.");

        var category = new Category
        {
            UserId = UserId,
            Name = request.Name.Trim(),
            ColorHex = request.ColorHex,
            Icon = request.Icon,
            DefaultNature = request.DefaultNature,
            ParentCategoryId = request.ParentCategoryId,
            IsSystem = false
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        return Map(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new AppValidationException(nameof(request.Name), "O nome da categoria é obrigatório.");

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct)
            ?? throw NotFoundException.For("Categoria", id);
        category.Name = request.Name.Trim();
        category.ColorHex = request.ColorHex;
        category.Icon = request.Icon;
        category.DefaultNature = request.DefaultNature;
        category.ParentCategoryId = request.ParentCategoryId;
        category.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Map(category);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && c.UserId == UserId, ct)
            ?? throw NotFoundException.For("Categoria", id);

        var inUse = await _db.Transactions.AnyAsync(t => t.UserId == UserId && t.CategoryId == id, ct);
        if (inUse)
        {
            category.IsArchived = true; // preserva o histórico
            category.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _db.Categories.Remove(category);
        }
        await _db.SaveChangesAsync(ct);
    }

    private static CategoryDto Map(Category c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ColorHex = c.ColorHex,
        Icon = c.Icon,
        DefaultNature = c.DefaultNature,
        NatureLabel = c.DefaultNature.GetDescription(),
        IsSystem = c.IsSystem,
        IsArchived = c.IsArchived,
        ParentCategoryId = c.ParentCategoryId
    };
}
