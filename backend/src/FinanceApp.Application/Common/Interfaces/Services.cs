using FinanceApp.Domain.Entities;

namespace FinanceApp.Application.Common.Interfaces;

/// <summary>Fornece a data/hora atual (abstrai <c>DateTime.UtcNow</c> para permitir testes determinísticos).</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
}

/// <summary>Representa o usuário autenticado na requisição atual.</summary>
public interface ICurrentUser
{
    /// <summary>Id do usuário autenticado, ou <c>null</c> em requisições anônimas.</summary>
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}

/// <summary>Gera e verifica hashes de senha (PBKDF2). Nunca trafega/armazena a senha em texto puro.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Emite tokens de acesso (JWT) para o usuário autenticado.</summary>
public interface ITokenService
{
    (string accessToken, DateTime expiresAtUtc) CreateAccessToken(User user);
}
