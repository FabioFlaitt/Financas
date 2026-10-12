using FinanceApp.Application.Categories;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Interfaces;
using FinanceApp.Domain.Entities;
using FinanceApp.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceApp.Application.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);

public record AuthResult
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public DateTime ExpiresAtUtc { get; init; }
}

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(IApplicationDbContext db, IPasswordHasher hasher, ITokenService tokens)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new AppValidationException(nameof(request.Email), "E-mail inválido.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new AppValidationException(nameof(request.Password), "A senha deve ter ao menos 8 caracteres.");
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new AppValidationException(nameof(request.Email), "Já existe uma conta com este e-mail.");

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password)
        };
        _db.Users.Add(user);

        // Semeia as categorias padrão e as preferências de notificação.
        foreach (var category in DefaultCategories.CreateFor(user.Id))
            _db.Categories.Add(category);
        foreach (NotificationType type in Enum.GetValues<NotificationType>())
            _db.NotificationPreferences.Add(new NotificationPreference { UserId = user.Id, Type = type });

        await _db.SaveChangesAsync(ct);

        var (token, expires) = _tokens.CreateAccessToken(user);
        return new AuthResult
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            AccessToken = token,
            ExpiresAtUtc = expires
        };
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new AppValidationException("Credenciais", "E-mail ou senha inválidos.");

        var (token, expires) = _tokens.CreateAccessToken(user);
        return new AuthResult
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email,
            AccessToken = token,
            ExpiresAtUtc = expires
        };
    }
}
