using NotificationService.Domain;

namespace NotificationService.Channels;

/// <summary>
///     Stub implementation — replace with a real SMTP client when mail settings are available.
/// </summary>
public class MailChannel : INotificationChannel
{
    private readonly ILogger<MailChannel> _logger;

    public MailChannel(ILogger<MailChannel> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(NotificationMessage message)
    {
        foreach (string email in message.RecipientEmails)
            _logger.LogInformation(
                "[STUB] Sending mail to {Email}: [{Series}] {Summary}",
                email, message.SeriesName, message.ChangeSummary);

        return Task.CompletedTask;
    }
}