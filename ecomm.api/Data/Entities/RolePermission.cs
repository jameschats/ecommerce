namespace ecomm.api.Data.Entities;

public class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
