using Alexandria.Services.Storage.Content.Mime;
using Microsoft.Extensions.DependencyInjection;

namespace Alexandria.Infrastructure;

public static class MimeDetectionExtensions
{
    /// <summary>
    /// Registers the MIME sniffer and resolver as singletons. Both are stateless and thread safe,
    /// and the sniffer builds its content inspector once because building is expensive.
    /// </summary>
    public static IServiceCollection AddMimeDetection(this IServiceCollection services)
    {
        services.AddSingleton<IMimeSniffer, MimeDetectiveSniffer>();
        services.AddSingleton<IMimeTypeResolver, MimeTypeResolver>();
        return services;
    }
}
