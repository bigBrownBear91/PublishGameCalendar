using System.Text;
using System.Text.Json;
using PublishGameCalendar.Domain;
using RabbitMQ.Client;

namespace PublishGameCalendar.Services.Queue;

public class RabbitMqQueueAdapter : IQueueAdapter, IAsyncDisposable
{
    private const string QueueName = "notifications";

    private readonly IConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private IChannel? _channel;
    private IConnection? _connection;

    public RabbitMqQueueAdapter(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.CloseAsync();
        if (_connection is not null) await _connection.CloseAsync();
        _lock.Dispose();
    }

    public async Task PublishAsync(NotificationMessage message)
    {
        IChannel channel = await GetChannelAsync();
        byte[] body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        await channel.BasicPublishAsync(string.Empty, QueueName, body);
    }

    private async Task<IChannel> GetChannelAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_connection is null || !_connection.IsOpen)
            {
                _connection = await _connectionFactory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();
                await _channel.QueueDeclareAsync(QueueName, false, false, false);
            }

            return _channel!;
        }
        finally
        {
            _lock.Release();
        }
    }
}