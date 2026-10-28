using MessageBrokerDemo.Broker;
using MessageBrokerDemo.Data;
using MessageBrokerDemo.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMqBroker(builder.Configuration);
builder.Services.Configure<OutboxPublisherOptions>(builder.Configuration.GetSection(OutboxPublisherOptions.SectionName));
builder.Services.AddHostedService<OutboxPublisherWorker>();

var host = builder.Build();
host.Run();
