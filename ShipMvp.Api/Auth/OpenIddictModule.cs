using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using ShipMvp.Core.Attributes;
using ShipMvp.Core.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using ShipMvp.Application.Infrastructure.Data;
using System;

namespace ShipMvp.Api.Auth;

[Module]
public class OpenIddictModule : IModule
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Configure OpenIddict with EF Core stores using the existing AppDbContext
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                       .UseDbContext<AppDbContext>();
            })
            .AddServer(options =>
            {
                // Enable token endpoint, allow password/client flows as needed
                options.SetTokenEndpointUris("/connect/token");

                // For development: allow password and refresh token flows
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();

                // Accept incoming requests from the browser (if needed)
                options.AcceptAnonymousClients();

                // During development use symmetric encryption/signing
                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                // Configure scopes
                options.RegisterScopes(OpenIddictConstants.Scopes.Roles, OpenIddictConstants.Scopes.Email, OpenIddictConstants.Scopes.Profile);

                // Disable HTTPS transport requirement for local development only
                // Enable pass-through mode to allow custom TokenController to handle requests
                options.UseAspNetCore()
                       .DisableTransportSecurityRequirement()
                       .EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                // Use the default validation settings which will use the local OpenIddict server
                options.UseLocalServer();
                options.UseAspNetCore();
            });
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        // OpenIddict client seeding is handled by the centralized DataSeeder.
    }
}
