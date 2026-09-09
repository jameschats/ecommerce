namespace ecomm.api.Common.Security;

/// <summary>
/// Permission codes, matching the Code column in the Permissions table.
///
/// Constants rather than raw strings at twenty call sites: a typo in an [Authorize(Policy)]
/// attribute does not fail to compile, it fails to authorise — and the symptom is a screen
/// nobody can reach, discovered by whoever it locks out.
/// </summary>
public static class Perm
{
    public const string CatalogView = "catalog.view";
    public const string CatalogManage = "catalog.manage";
    public const string InventoryView = "inventory.view";
    public const string InventoryManage = "inventory.manage";
    public const string OrderView = "order.view";
    public const string OrderManage = "order.manage";
    public const string CustomerView = "customer.view";
    public const string CustomerManage = "customer.manage";
    public const string PaymentVerify = "payment.verify";
    public const string CouponManage = "coupon.manage";
    public const string ReviewModerate = "review.moderate";
    public const string CmsManage = "cms.manage";
    public const string ThemeManage = "theme.manage";
    public const string SettingsManage = "settings.manage";
    /// <summary>Narrower than SettingsManage: just the admin Notifications screen, so a role
    /// can see notifications without also getting Store/Shop settings, templates, sign-in
    /// methods and Go-live/Data-reset.</summary>
    public const string SettingsNotifications = "settings.notifications";
    public const string MediaManage = "media.manage";
    public const string ImportManage = "import.manage";
    public const string ReportView = "report.view";
    public const string UserManage = "user.manage";
    public const string RoleManage = "role.manage";

    /// <summary>Every code, so policies can be registered without listing them twice.</summary>
    public static readonly string[] All =
    [
        CatalogView, CatalogManage, InventoryView, InventoryManage,
        OrderView, OrderManage, CustomerView, CustomerManage, PaymentVerify,
        CouponManage, ReviewModerate, CmsManage, ThemeManage, SettingsManage,
        SettingsNotifications, MediaManage, ImportManage, ReportView, UserManage, RoleManage,
    ];

    /// <summary>The claim type the JWT carries these in (JwtTokenService).</summary>
    public const string ClaimType = "perm";
}
