using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace ShipMvp.Application.Infrastructure.Security;

/// <summary>
/// Default implementation of <see cref="ICurrentUser"/> that reads from <see cref="IHttpContextAccessor"/>.
/// Uses OpenIddict claims exclusively.
/// </summary>
public sealed class CurrentUser : ShipMvp.Core.Security.ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CurrentUser> _logger;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, ILogger<CurrentUser> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated 
    { 
        get 
        {
            var isAuth = Principal?.Identity?.IsAuthenticated == true;
            _logger.LogDebug("CurrentUser.IsAuthenticated: {IsAuthenticated}, Principal: {Principal}, Identity: {Identity}, IdentityType: {IdentityType}, AuthenticationType: {AuthenticationType}", 
                isAuth, Principal != null, Principal?.Identity != null, Principal?.Identity?.GetType().Name, Principal?.Identity?.AuthenticationType);
            return isAuth;
        }
    }

    public Guid? Id
    {
        get
        {
            // Use OpenIddict Subject claim only
            var idValue = this[OpenIddictConstants.Claims.Subject];
            var result = Guid.TryParse(idValue, out var id) ? id : (Guid?)null;
            _logger.LogDebug("CurrentUser.Id: {Id}, IdValue: {IdValue}, Principal: {Principal}", 
                result, idValue, Principal != null);
            return result;
        }
    }

    public string? UserName 
    { 
        get 
        {
            // Use OpenIddict Username claim only
            return this[OpenIddictConstants.Claims.Username];
        }
    }

    public string? Email 
    { 
        get 
        {
            // Use OpenIddict Email claim only
            return this[OpenIddictConstants.Claims.Email];
        }
    }

    public IReadOnlyList<string> Roles 
    { 
        get 
        {
            // Get roles from OpenIddict claims only
            return Principal?.FindAll(OpenIddictConstants.Claims.Role).Select(c => c.Value).ToList() ?? new List<string>();
        }
    }

    public IEnumerable<Claim> Claims => Principal?.Claims ?? Array.Empty<Claim>();

    public string? this[string claimType] => Principal?.FindFirst(claimType)?.Value;
}