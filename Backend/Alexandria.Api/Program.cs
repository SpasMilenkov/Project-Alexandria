using Alexandria.Common.Health;
using Alexandria.Api.Extensions;
using Alexandria.Api.Features.Auth.Extensions;
using Alexandria.Api.Middlewares;
using Alexandria.Infrastructure;
using Alexandria.Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var probeExitCode = await HttpHealthProbe.TryRunAsync(args);
if (probeExitCode is { } exitCode)
{
    Environment.ExitCode = exitCode;
    return;
}

var bld = WebApplication.CreateBuilder();


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

bld.Services
    .AddHttpContextAccessor()
    .AddDatabase(bld.Configuration)
    .AddAuthAndIdentity()
    .AddJwtAuthentication(bld.Configuration)
    .AddAuthorizationPolicies()
    .AddS3Storage(bld.Configuration)
    .AddRabbitMqAsync(bld.Configuration)
    .AddApiServices(bld.Configuration)
    .AddServices()
    .AddAutoTagging(bld.Configuration)
    .AddHealthMonitoring(bld.Configuration)
    .AddAuthServices()
    .AddTelemetry();

bld.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
);
try
{
    var app = bld.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("Host", httpContext.Request.Host.Value);
            diagnosticContext.Set("Protocol", httpContext.Request.Protocol);
        };
    });

    if (!app.Configuration.GetValue<bool>("SkipDatabaseInit"))
    {
        await app.InitializeDatabaseAsync();
    }

    app.UseForwardedHeaders();
    app.UseCors("AllowOrigin");
    app.UseMiddleware<JwtFromCookieMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseResponseCaching();
    app.UseAlexandriaEndpoints();


    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = HealthMonitoringExtension.IsEssentialReadinessCheck,
    });
    app.MapPrometheusScrapingEndpoint();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program
{
}
