using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using MessageBrokerDemo.Data.Persistence;

namespace MessageBrokerDemo.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // default scoped lifetime for DbContext is Scoped
        services.AddDbContext<WCMSDBContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres") ?? "Host=localhost;Port=5432;Database=wcmsdemo;Username=postgres;Password=postgres"));

        // todo: configure S3 storage options
        //services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));

        return services;
    }
}
