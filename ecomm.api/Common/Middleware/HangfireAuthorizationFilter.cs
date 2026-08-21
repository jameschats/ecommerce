using Hangfire.Dashboard;

namespace ecomm.api.Common.Middleware;

/// <summary>Gates the Hangfire dashboard (/admin/jobs) to SuperAdmin only — Hangfire storage is shared
/// platform-wide (jobs aren't tenant-scoped), so a per-tenant Admin seeing another store's job/queue
/// details would be a real data-boundary leak. Same posture as the platform Audit Log and Staff pages.</summary>
public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true && http.User.IsInRole("SuperAdmin");
    }
}
