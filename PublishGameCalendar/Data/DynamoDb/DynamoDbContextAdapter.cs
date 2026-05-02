using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;

namespace PublishGameCalendar.Data.DynamoDb;

public class DynamoDbContextAdapter : IDynamoDbContext
{
    private readonly DynamoDBContext _context;

    public DynamoDbContextAdapter(DynamoDBContext context)
    {
        _context = context;
    }

    public Task<T?> LoadAsync<T>(object hashKey, CancellationToken ct = default) =>
        _context.LoadAsync<T>(hashKey, ct)!;

    public Task<T?> LoadAsync<T>(object hashKey, object rangeKey, CancellationToken ct = default) =>
        _context.LoadAsync<T>(hashKey, rangeKey, ct)!;

    public Task SaveAsync<T>(T value, CancellationToken ct = default) =>
        _context.SaveAsync(value, ct);

    public Task DeleteAsync<T>(object hashKey, CancellationToken ct = default) =>
        _context.DeleteAsync<T>(hashKey, ct);

    public Task DeleteAsync<T>(object hashKey, object rangeKey, CancellationToken ct = default) =>
        _context.DeleteAsync<T>(hashKey, rangeKey, ct);

    public async Task<List<T>> ScanAsync<T>(IEnumerable<ScanCondition> conditions) =>
        await _context.ScanAsync<T>(conditions).GetRemainingAsync();

    public async Task<List<T>> QueryAsync<T>(object hashKey) =>
        await _context.QueryAsync<T>(hashKey).GetRemainingAsync();
}
