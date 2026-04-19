namespace PublishGameCalendar.Services.Pollers;

public class PollerFactory : IPollerFactory
{
    private readonly IServiceProvider _provider;

    public PollerFactory(IServiceProvider provider)
    {
        _provider = provider;
    }

    public IWebsitePoller Create(string pollerType)
    {
        return pollerType switch
        {
            nameof(StubPoller) => _provider.GetRequiredService<StubPoller>(),
            nameof(DoubleRoundRobinPoller) => _provider.GetRequiredService<DoubleRoundRobinPoller>(),
            _ => throw new NotSupportedException($"Poller type '{pollerType}' is not registered.")
        };
    }
}