using System.Text;
using System.Text.Json;
using NotificationService.Domain;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService;

public class QueueConsumer : BackgroundService
{
    private const string QueueName = "notifications";

    private readonly IConnectionFactory _connectionFactory;
    private readonly NotificationDispatcher _dispatcher;
    private readonly ILogger<QueueConsumer> _logger;

    public QueueConsumer(
        IConnectionFactory connectionFactory,
        NotificationDispatcher dispatcher,
        ILogger<QueueConsumer> logger)
    {
        _connectionFactory = connectionFactory;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IConnection connection = await ConnectWithRetryAsync(stoppingToken);
        using IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(QueueName, false, false, false,
            cancellationToken: stoppingToken);

        AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                string body = Encoding.UTF8.GetString(ea.Body.ToArray());
                NotificationMessage? message = JsonSerializer.Deserialize<NotificationMessage>(body);
                if (message is null)
                {
                    _logger.LogError("Received undeserializable notification message. Body: {Body}", body);
                    await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                    return;
                }

                await _dispatcher.DispatchAsync(message);
                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification message.");
                await channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await channel.BasicConsumeAsync(QueueName, false, consumer,
            stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task<IConnection> ConnectWithRetryAsync(CancellationToken cancellationToken)
    {
        int[] delaysSeconds = [2, 5, 10, 20, 30];
        for (int i = 0; i <= delaysSeconds.Length; i++)
        {
            try
            {
                return await _connectionFactory.CreateConnectionAsync(cancellationToken);
            }
            catch (Exception ex) when (i < delaysSeconds.Length)
            {
                _logger.LogWarning(ex, "RabbitMQ not ready, retrying in {Delay}s…", delaysSeconds[i]);
                await Task.Delay(TimeSpan.FromSeconds(delaysSeconds[i]), cancellationToken);
            }
        }
        return await _connectionFactory.CreateConnectionAsync(cancellationToken);
    }
}