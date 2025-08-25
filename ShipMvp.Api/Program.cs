using ShipMvp.Api;
using ShipMvp.Core.Modules;

var builder = WebApplication.CreateBuilder(args);

// Ensure Data Protection is registered early so services depending on IDataProtectionProvider can be validated
builder.Services.AddDataProtection();

// Add HostModule type, and let ModuleContainer resolve dependencies like ApiModule
builder.Services.AddModules(
    typeof(HostModule)
);

try
{
    var app = builder.Build();

    // Configure modules (this will apply other middleware)
    app.ConfigureModules(app.Environment);

    app.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Error during app build: {ex.Message}");
    throw;
}
