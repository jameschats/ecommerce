namespace ecomm.api.Common.Tenancy;

/// <summary>
/// Marks an entity whose rows belong to a single tenant (store). Entities that
/// implement this get an automatic EF Core global query filter
/// (WHERE TenantId = current tenant) and have TenantId auto-stamped on insert.
///
/// NOT everything with a TenantId column is tenant-scoped — Role/Permission are
/// platform-global and deliberately do NOT implement this. See design-v2.md §6.1.
/// </summary>
public interface ITenantScoped
{
    long TenantId { get; set; }
}
