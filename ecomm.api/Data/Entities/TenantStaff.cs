namespace ecomm.api.Data.Entities;

/// <summary>
/// A staff member of a tenant: a <see cref="User"/> in the ADMIN role, with a per-tenant
/// access level (Owner | Admin | Staff | Viewer) and status. The ADMIN role grants panel
/// access; this row governs what they can do (Viewer = read-only; Staff can't manage staff).
/// </summary>
public class TenantStaff : ITenantScoped
{
    public long TenantStaffId { get; set; }
    public long TenantId { get; set; } = 1;
    public long UserId { get; set; }
    public string AccessLevel { get; set; } = StaffAccess.Staff;
    public string Status { get; set; } = StaffStatus.Active;
    public long? InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class StaffAccess
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Viewer = "Viewer";
    public static readonly string[] All = { Owner, Admin, Staff, Viewer };
    /// <summary>Levels allowed to manage other staff.</summary>
    public static bool CanManageStaff(string level) => level is Owner or Admin;
}

public static class StaffStatus
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";
}
