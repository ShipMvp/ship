using OpenIddict.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using ShipMvp.Core;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using ShipMvp.Domain.Shared.Constants;

namespace ShipMvp.Api.Auth;

[Module]
[DependsOn<OpenIddictModule>]
public class AuthorizationModule : IModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add Data Protection services (required for encryption)
        services.AddDataProtection();
        
        // Configure authentication to use OpenIddict validation only
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        // Add authorization services
        services.AddAuthorization(options =>
        {
            // Configure policies based on roles
            options.AddPolicy(Policies.RequireAdminRole, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.RequireBillingAccess, policy =>
                policy.RequireRole(Roles.Admin, Roles.BillingManager));

            options.AddPolicy(Policies.RequireUserManagement, policy =>
                policy.RequireRole(Roles.Admin, Roles.Support));

            options.AddPolicy(Policies.RequireReadOnly, policy =>
                policy.RequireRole(Roles.Admin, Roles.User, Roles.BillingManager, Roles.Support, Roles.ReadOnly));

            // Default policy requires authenticated user
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        // Add authentication middleware before authorization
        app.UseAuthentication();
        app.UseAuthorization();
    }
}
