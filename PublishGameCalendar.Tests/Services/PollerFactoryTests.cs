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
        services.AddTransient<StubPoller>();
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
    public void Create_WithUnknownType_ThrowsNotSupportedException()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => _sut.Create("UnknownPoller"));
    }
}