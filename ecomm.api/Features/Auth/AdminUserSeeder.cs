using ecomm.api.Data.Context;
using ecomm.api.Features.Auth.Services;
using Microsoft.EntityFrameworkCore;

namespace ecomm.api.Features.Auth;

/// <summary>
/// On startup, replaces the Stage-0 admin placeholder hash (`SET_BY_AUTH_MODULE`)
/// with a real BCrypt hash so the admin can sign in. The seed password/email can be
/// overridden via config (<c>Admin:DefaultPassword</c> / <c>Admin:Email</c>) so a fresh
/// production deploy seeds a strong password instead of the well-known default.
/// </summary>
public sealed class AdminUserSeeder : IHostedService
{
    public const string PlaceholderHash = "SET_BY_AUTH_MODULE";
    public const string AdminNormalizedEmail = "ADMIN@ECOMMERCE.LOCAL";
    public const string DefaultAdminPassword = "Admin@123";

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminUserSeeder> _logger;

    public AdminUserSeeder(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<AdminUserSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EcommerceDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var normalizedEmail = (_configuration["Admin:Email"] ?? AdminNormalizedEmail).ToUpperInvariant();
        var seedPassword = _configuration["Admin:DefaultPassword"] ?? DefaultAdminPassword;

        var admin = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
        if (admin is null) return;

        if (admin.PasswordHash is null or PlaceholderHash || admin.PasswordHash.Length == 0)
        {
            admin.PasswordHash = hasher.Hash(seedPassword);
            admin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            if (seedPassword == DefaultAdminPassword)
                _logger.LogWarning(
                    "Seeded admin {Email} with the DEFAULT password — change it immediately, or set Admin:DefaultPassword before first run.",
                    admin.Email);
            else
                _logger.LogInformation("Seeded admin {Email} from configured Admin:DefaultPassword.", admin.Email);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
