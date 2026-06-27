using ecomm.api.Data.Context;
using ecomm.api.Features.Auth.Services;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Auth;

/// <summary>
/// On startup, replaces the Stage-0 admin placeholder hash (`SET_BY_AUTH_MODULE`)
/// with a real BCrypt hash so the admin can sign in.
/// </summary>
public sealed class AdminUserSeeder : IHostedService
{
    public const string PlaceholderHash = "SET_BY_AUTH_MODULE";
    public const string AdminNormalizedEmail = "ADMIN@ECOMMERCE.LOCAL";
    public const string DefaultAdminPassword = "Admin@123";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminUserSeeder> _logger;

    public AdminUserSeeder(IServiceProvider serviceProvider, ILogger<AdminUserSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == AdminNormalizedEmail, cancellationToken);
        if (admin is null) return;

        if (admin.PasswordHash is null or PlaceholderHash || admin.PasswordHash.Length == 0)
        {
            admin.PasswordHash = hasher.Hash(DefaultAdminPassword);
            admin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Seeded admin password for {Email}. Default is '{Password}' — change it immediately.",
                admin.Email, DefaultAdminPassword);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
