using Nop.Core.Domain.Customers;

namespace Nop.Services.Security;

/// <summary>
/// Permission service interface
/// </summary>
public partial interface IPermissionService
{
    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - authorized; otherwise, false
    /// </returns>
    Task<bool> AuthorizeAsync(string permissionRecordSystemName);

    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - authorized; otherwise, false
    /// </returns>
    Task<bool> AuthorizeAsync(string permissionRecordSystemName, Customer customer);

    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <param name="customerRoleId">Customer role identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains true - authorized; otherwise, false
    /// </returns>
    Task<bool> AuthorizeAsync(string permissionRecordSystemName, int customerRoleId);
}