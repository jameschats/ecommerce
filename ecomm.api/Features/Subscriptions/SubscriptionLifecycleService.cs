namespace ecomm.api.Features.Subscriptions;

/// <summary>
/// Periodically sweeps subscriptions: trial/period ended → PastDue + grace;
/// past grace → Suspended (store 404s). Lightweight timer for now — moves to
/// Hangfire in V2-6 when the platform gains more background jobs.
/// </summary>
public sealed class SubscriptionLifecycleService(
    IServiceProvider services, IConfiguration config, ILogger<SubscriptionLifecycleService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = config.GetValue("Billing:SweepIntervalHours", 6);
        var graceDays = config.GetValue("Billing:GraceDays", 3);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(0.1, intervalHours)));

        do
        {
            try
            {
                using var scope = services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                var n = await svc.RunLifecycleSweepAsync(DateTime.UtcNow, graceDays, stoppingToken);
                if (n > 0) logger.LogInformation("Subscription lifecycle sweep: {Count} transition(s).", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Subscription lifecycle sweep failed."); }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
