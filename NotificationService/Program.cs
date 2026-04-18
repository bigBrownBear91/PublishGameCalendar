using NotificationService;
using NotificationService.Channels;
using RabbitMQ.Client;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IConnectionFactory>(_ =>
{
    IConfigurationSection rmq = builder.Configuration.GetSection("RabbitMq");
    return new ConnectionFactory
    {
        HostName = rmq["Host"] ?? "rabbitmq",
        Port = int.Parse(rmq["Port"] ?? "5672"),
        UserName = rmq["Username"] ?? "guest",
        Password = rmq["Password"] ?? "guest"
    };
});

builder.Services.AddSingleton<INotificationChannel, MailChannel>();
builder.Services.AddSingleton<NotificationDispatcher>();
builder.Services.AddHostedService<QueueConsumer>();

IHost host = builder.Build();
await host.RunAsync();