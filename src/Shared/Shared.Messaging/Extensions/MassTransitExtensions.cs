using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Shared.Messaging.Extensions;
public static class MassTransitExtentions
{
    public static IServiceCollection AddMassTransitWithAssemblies
        (this IServiceCollection services, IConfiguration configuration, params Assembly[] assemblies)
    {
        services.AddMassTransit(config =>
        {
            config.SetKebabCaseEndpointNameFormatter();

            config.SetInMemorySagaRepositoryProvider();

            ////config.AddConsumers(assemblies);
            config.AddSagaStateMachines(assemblies);
            config.AddSagas(assemblies);
            config.AddActivities(assemblies);
            /*
            config.UsingInMemory((context, configurator) =>);
            {
                configurator.ConfigureEndpoints(context);
            });
            */
            config.UsingRabbitMq((context, configurator) =>
            {
                configurator.Host(new Uri(configuration["MessageBroker:Host"]!), host =>
                {
                    host.Username(configuration["MessageBroker:UserName"]!);
                    host.Password(configuration["MessageBroker:Password"]!);
                });
                /*
                // Konfigurasi Exchange dengan tipe 'topic'
                configurator.ExchangeType = "topic";  // Menetapkan exchange type sebagai 'topic'

                // Mengonfigurasi routing key untuk topik tertentu
                configurator.ReceiveEndpoint("your_topic_queue", e =>
                {
                    // Menentukan routing key untuk menerima pesan dengan topik tertentu
                    e.Bind("your_topic_exchange", x =>
                    {
                        // Menentukan routing key untuk mengirim pesan berdasarkan topik
                        x.RoutingKey = "your.routing.key.#";  // Routing key dengan wildcard (# untuk mencocokkan semua)
                    });

                    e.ConfigureConsumers(context);
                });
                */
                configurator.ConfigureEndpoints(context);
            });
            
            
        });

        return services;
    }
}
