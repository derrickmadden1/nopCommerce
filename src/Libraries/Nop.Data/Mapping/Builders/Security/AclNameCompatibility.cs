using Nop.Core.Domain.Security;

namespace Nop.Data.Mapping.Builders.Security;

/// <summary>
/// ACL instance of backward compatibility of table naming
/// </summary>
public class AclNameCompatibility : INameCompatibility
{
    public Dictionary<Type, string> TableNames => new()
    {
        { typeof(PermissionRecordCustomerRoleMapping), "PermissionRecord_Role_Mapping" }
    };

    public Dictionary<(Type, string), string> ColumnName => new()
    {
        { (typeof(PermissionRecordCustomerRoleMapping), "PermissionRecordId"), "PermissionRecord_Id" },
        { (typeof(PermissionRecordCustomerRoleMapping), "CustomerRoleId"), "CustomerRole_Id" }
    };
}
