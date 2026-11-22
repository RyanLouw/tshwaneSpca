using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TshwaneSPCA.ServiceDefaults;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks();
        return builder;
    }

    public static void MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
    }
}
