using Microsoft.Extensions.DependencyInjection;

namespace MessageBrokerDemo.Service;

public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IUploadService, UploadService>();
        return services;
    }
}
