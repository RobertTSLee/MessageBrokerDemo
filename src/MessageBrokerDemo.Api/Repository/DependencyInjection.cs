using Microsoft.Extensions.DependencyInjection;

namespace MessageBrokerDemo.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUploadRepository, UploadRepository>();
        return services;
    }
}
