using Nop.Core.Domain.Security;

namespace Nop.Services.Security;

/// <summary>
/// Represents a permission manager
/// </summary>
public interface IPermissionManager
{
    /// <summary>
    /// Gets a permission record by identifier
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains a permission record
    /// </returns>
    Task<PermissionRecord> GetPermissionRecordByIdAsync(int permissionId);

    /// <summary>
    /// Gets all permissions
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permissions
    /// </returns>
    Task<IList<PermissionRecord>> GetAllPermissionRecordsAsync();

    /// <summary>
    /// Updates the permission
    /// </summary>
    /// <param name="permission">Permission</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task UpdatePermissionRecordAsync(PermissionRecord permission);

    /// <summary>
    /// Delete a permission
    /// </summary>
    /// <param name="permissionSystemName">Permission system name</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeletePermissionAsync(string permissionSystemName);

    /// <summary>
    /// Gets a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<IList<PermissionRecordCustomerRoleMapping>> GetMappingByPermissionRecordIdAsync(int permissionId);

    /// <summary>
    /// Delete a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <param name="customerRoleId">Customer role identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task DeletePermissionRecordCustomerRoleMappingAsync(int permissionId, int customerRoleId);

    /// <summary>
    /// Inserts a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionRecordCustomerRoleMapping">Permission record-customer role mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertPermissionRecordCustomerRoleMappingAsync(PermissionRecordCustomerRoleMapping permissionRecordCustomerRoleMapping);

    /// <summary>
    /// Configure permission manager
    /// </summary>
    void Configure();

    /// <summary>
    /// Inserts a permission record-customer role mappings
    /// </summary>
    /// <param name="customerRoleId">Customer role ID</param>
    /// <param name="permissions">Permissions</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task InsertPermissionMappingAsync(int customerRoleId, params string[] permissions);
}