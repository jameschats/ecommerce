namespace ecomm.api.Features.Auth.Services;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken ct = default);
}

/// <summary>
/// Dev/stub SMS sender — logs the message instead of sending.
/// Swap for a real gateway (MSG91, Twilio, …) by implementing ISmsSender.
/// </summary>
public sealed class ConsoleSmsSender : ISmsSender
{
    private readonly ILogger<ConsoleSmsSender> _logger;

    public ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) => _logger = logger;

    public Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        _logger.LogWarning("[DEV SMS] To {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
