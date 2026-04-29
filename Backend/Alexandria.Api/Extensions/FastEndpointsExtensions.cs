using Alexandria.Common.Auth;
using Alexandria.Infrastructure.Converters;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Alexandria.Api.Extensions;

public static class FastEndpointsExtensions
{
    public static IApplicationBuilder UseAlexandriaEndpoints(this WebApplication app)
    {
        app.UseFastEndpoints(c =>
        {
            c.Versioning.Prefix = "v";
            c.Endpoints.RoutePrefix = "api";
            c.Versioning.PrependToRoute = true;
            c.Serializer.Options.Converters.Add(new BigIntegerJsonConverter());
            c.Endpoints.Configurator = ep =>
            {
                ep.AuthSchemes(JwtBearerDefaults.AuthenticationScheme);

                if (ep.AnonymousVerbs is null || ep.AnonymousVerbs.Length == 0)
                    ep.Policies(Policies.RequireUser);
            };
        }).UseSwaggerGen();

        return app;
    }
}