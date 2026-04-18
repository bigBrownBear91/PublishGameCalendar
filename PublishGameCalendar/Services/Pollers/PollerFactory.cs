namespace PublishGameCalendar.Services.Pollers;

public class PollerFactory
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
            _ => throw new NotSupportedException($"Poller type '{pollerType}' is not registered.")
        };
    }
}