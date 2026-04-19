namespace PublishGameCalendar.Services.Pollers;

public interface IPollerFactory
{
    IWebsitePoller Create(string pollerType);
}
