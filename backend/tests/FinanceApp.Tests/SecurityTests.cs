using FinanceApp.Application.Auth;
using FinanceApp.Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace FinanceApp.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_then_verify_succeeds()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Senha@Forte123");

        hash.Should().NotContain("Senha@Forte123"); // nunca em texto puro
        hasher.Verify("Senha@Forte123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("Senha@Forte123");

        hasher.Verify("senhaerrada", hash).Should().BeFalse();
    }

    [Fact]
    public void Two_hashes_of_same_password_use_different_salts()
    {
        var hasher = new PasswordHasher();
        hasher.Hash("igual").Should().NotBe(hasher.Hash("igual"));
    }
}

public class AuthServiceTests
{
    [Fact]
    public async Task Register_seeds_default_categories_and_hashes_password()
    {
        var db = TestDb.NewContext();
        var service = new AuthService(db, new PasswordHasher(), new FakeTokenService());

        var result = await service.RegisterAsync(new RegisterRequest("Fulano", "Fulano@Email.com", "Senha@Forte123"));

        result.AccessToken.Should().Be("fake-token");
        db.Users.Single().Email.Should().Be("fulano@email.com"); // normalizado
        db.Categories.Count().Should().Be(14); // categorias padrão semeadas
    }

    [Fact]
    public async Task Register_rejects_duplicate_email()
    {
        var db = TestDb.NewContext();
        var service = new AuthService(db, new PasswordHasher(), new FakeTokenService());
        await service.RegisterAsync(new RegisterRequest("A", "a@a.com", "Senha@Forte123"));

        var act = () => service.RegisterAsync(new RegisterRequest("B", "a@a.com", "Senha@Forte123"));

        await act.Should().ThrowAsync<FinanceApp.Application.Common.Exceptions.AppValidationException>();
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials()
    {
        var db = TestDb.NewContext();
        var service = new AuthService(db, new PasswordHasher(), new FakeTokenService());
        await service.RegisterAsync(new RegisterRequest("A", "a@a.com", "Senha@Forte123"));

        var result = await service.LoginAsync(new LoginRequest("a@a.com", "Senha@Forte123"));
        result.Email.Should().Be("a@a.com");
    }

    [Fact]
    public async Task Login_fails_with_wrong_password()
    {
        var db = TestDb.NewContext();
        var service = new AuthService(db, new PasswordHasher(), new FakeTokenService());
        await service.RegisterAsync(new RegisterRequest("A", "a@a.com", "Senha@Forte123"));

        var act = () => service.LoginAsync(new LoginRequest("a@a.com", "errada"));
        await act.Should().ThrowAsync<FinanceApp.Application.Common.Exceptions.AppValidationException>();
    }

    [Theory]
    [InlineData("curta1")]        // menos de 8 caracteres
    [InlineData("semnumeros")]    // sem dígito
    [InlineData("12345678")]      // sem letra
    public async Task Register_rejects_weak_password(string password)
    {
        var db = TestDb.NewContext();
        var service = new AuthService(db, new PasswordHasher(), new FakeTokenService());

        var act = () => service.RegisterAsync(new RegisterRequest("A", "a@a.com", password));

        await act.Should().ThrowAsync<FinanceApp.Application.Common.Exceptions.AppValidationException>();
    }

    private class FakeTokenService : FinanceApp.Application.Common.Interfaces.ITokenService
    {
        public (string accessToken, DateTime expiresAtUtc) CreateAccessToken(FinanceApp.Domain.Entities.User user)
            => ("fake-token", DateTime.UtcNow.AddHours(1));
    }
}
