using Amazon.DynamoDBv2.DataModel;

namespace PublishGameCalendar.Data.DynamoDb;
// ReSharper disable once TypeParameterCanBeVariant

public interface IDynamoDbContext
{
    Task<T?> LoadAsync<T>(object hashKey, CancellationToken ct = default);
    Task<T?> LoadAsync<T>(object hashKey, object rangeKey, CancellationToken ct = default);
    Task SaveAsync<T>(T value, CancellationToken ct = default);
    Task DeleteAsync<T>(object hashKey, CancellationToken ct = default);
    Task DeleteAsync<T>(object hashKey, object rangeKey, CancellationToken ct = default);
    Task<List<T>> ScanAsync<T>(IEnumerable<ScanCondition> conditions);
    Task<List<T>> QueryAsync<T>(object hashKey);
}
