using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore;
using OpenIddict.Abstractions;
using Microsoft.Extensions.Logging;
using ShipMvp.Application.Identity;
using System.Security.Claims;
using System.Collections.Generic;
using System;

namespace ShipMvp.Api.Auth;

[ApiController]
[Route("connect")]
public class TokenController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<TokenController> _logger;

    public TokenController(IAuthService authService, ILogger<TokenController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("token")]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("The OpenIddict request is not available.");

        if (request.IsPasswordGrantType())
        {
            var username = request.Username ?? string.Empty;
            var password = request.Password ?? string.Empty;

            // Reuse existing auth service to validate credentials
            var loginResult = await _authService.LoginAsync(new LoginDto { Email = username, Password = password });

            if (!loginResult.Success || loginResult.User == null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // Create claims principal
            var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(OpenIddictConstants.Claims.Subject, loginResult.User.Id.ToString()),
                new System.Security.Claims.Claim(OpenIddictConstants.Claims.Email, loginResult.User.Email),
                new System.Security.Claims.Claim(OpenIddictConstants.Claims.Username, loginResult.User.Username)
            };

            foreach (var role in (loginResult.User.Roles ?? new List<string>()))
            {
                claims.Add(new System.Security.Claims.Claim(OpenIddictConstants.Claims.Role, role));
            }

            var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Set scopes and resources
            principal.SetScopes(new[] { OpenIddictConstants.Scopes.OpenId, OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Roles });
            principal.SetResources("resource_server");

            _logger.LogInformation("Issuing token for user {UserId}", loginResult.User.Id);

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return BadRequest("The specified grant type is not supported.");
    }
}
