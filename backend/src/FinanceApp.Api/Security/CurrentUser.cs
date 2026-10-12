using System.Security.Claims;
using FinanceApp.Application.Common.Interfaces;

namespace FinanceApp.Api.Security;

/// <summary>Resolve o usuário autenticado a partir das claims do token na requisição atual.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? UserId
    {
        get
        {
            var principal = _accessor.HttpContext?.User;
            var value = principal?.FindFirstValue("sub")
                        ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => UserId is not null;
}
