using MessageBrokerDemo.Broker;
using MessageBrokerDemo.Consumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddRabbitMqBroker(builder.Configuration);
builder.Services.AddRabbitMqConsumer<JobNotificationHandler>("job-notifications", "job.*.notification");

var host = builder.Build();
host.Run();
