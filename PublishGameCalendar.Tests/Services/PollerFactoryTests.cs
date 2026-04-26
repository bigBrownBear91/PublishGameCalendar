using Microsoft.Extensions.DependencyInjection;
using PublishGameCalendar.Services.Pollers;
using Xunit;

namespace PublishGameCalendar.Tests.Services;

public class PollerFactoryTests
{
    private readonly PollerFactory _sut;

    public PollerFactoryTests()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<StubPoller>();
        services.AddHttpClient<Poller1>();
        ServiceProvider provider = services.BuildServiceProvider();
        _sut = new PollerFactory(provider);
    }

    [Fact]
    public void Create_WithStubPollerType_ReturnsStubPoller()
    {
        // Act
        IWebsitePoller poller = _sut.Create(nameof(StubPoller));

        // Assert
        Assert.IsType<StubPoller>(poller);
    }

    [Fact]
    public void Create_WithPoller1Type_ReturnsPoller1()
    {
        // Act
        IWebsitePoller poller = _sut.Create(nameof(Poller1));

        // Assert
        Assert.IsType<Poller1>(poller);
    }

    [Fact]
    public void Create_WithUnknownType_ThrowsNotSupportedException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _sut.Create("UnknownPoller"));
    }
}