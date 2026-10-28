using MessageBrokerDemo.Broker;
using MessageBrokerDemo.Data;
using MessageBrokerDemo.Model.Messaging;
using MessageBrokerDemo.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddRabbitMqBroker(builder.Configuration);
builder.Services.AddRabbitMqConsumer<ProcessJobHandler>(MessageQueue.ProcessJob.ToQueueName(), MessageTopic.JobProcess.ToTopicName());

var host = builder.Build();
host.Run();
