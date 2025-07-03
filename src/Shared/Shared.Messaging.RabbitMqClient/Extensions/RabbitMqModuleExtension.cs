using Shared.Messaging.RabbitMqClient.Provider;

namespace Shared.Messaging.RabbitMqClient.Extension;

public static class RabbitMqModuleExtension {
    public static IServiceCollection AddRabbitMqModule(this IServiceCollection services, IConfiguration config) {
        services.AddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        return services;
    }
}

