using Nop.Core;
using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Security;
using Nop.Data;
using Nop.Services.Customers;

namespace Nop.Services.Security;

/// <summary>
/// Permission service
/// </summary>
public class PermissionService : IPermissionService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IPermissionManager _permissionManager;
    protected readonly IRepository<PermissionRecord> _permissionRecordRepository;
    protected readonly IRepository<PermissionRecordCustomerRoleMapping> _permissionRecordCustomerRoleMappingRepository;
    protected readonly IStaticCacheManager _staticCacheManager;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public PermissionService(ICustomerService customerService,
        IPermissionManager permissionManager,
        IRepository<PermissionRecord> permissionRecordRepository,
        IRepository<PermissionRecordCustomerRoleMapping> permissionRecordCustomerRoleMappingRepository,
        IStaticCacheManager staticCacheManager,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _permissionManager = permissionManager;
        _permissionRecordRepository = permissionRecordRepository;
        _permissionRecordCustomerRoleMappingRepository = permissionRecordCustomerRoleMappingRepository;
        _staticCacheManager = staticCacheManager;
        _workContext = workContext;
    }

    #endregion

    #region Utilites

    /// <summary>
    /// Get permission records by customer role identifier
    /// </summary>
    /// <param name="customerRoleId">Customer role identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permissions
    /// </returns>
    protected virtual async Task<IList<PermissionRecord>> GetPermissionRecordsByCustomerRoleIdAsync(int customerRoleId)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(NopSecurityDefaults.PermissionRecordsAllCacheKey, customerRoleId);

        var query = from pr in _permissionRecordRepository.Table
            join prcrm in _permissionRecordCustomerRoleMappingRepository.Table on pr.Id equals prcrm
                .PermissionRecordId
            where prcrm.CustomerRoleId == customerRoleId
            orderby pr.Id
            select pr;

        return await _staticCacheManager.GetAsync(key, async () => await query.ToListAsync());
    }

    #endregion

    #region Methods

    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the true - authorized; otherwise, false
    /// </returns>
    public async Task<bool> AuthorizeAsync(string permissionRecordSystemName)
    {
        return await AuthorizeAsync(permissionRecordSystemName, await _workContext.GetCurrentCustomerAsync());
    }

    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <param name="customer">Customer</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the true - authorized; otherwise, false
    /// </returns>
    public async Task<bool> AuthorizeAsync(string permissionRecordSystemName, Customer customer)
    {
        if (string.IsNullOrEmpty(permissionRecordSystemName))
            return false;

        var customerRoles = await _customerService.GetCustomerRolesAsync(customer);
        foreach (var role in customerRoles)
            if (await AuthorizeAsync(permissionRecordSystemName, role.Id))
                //yes, we have such permission
                return true;

        //no permission found
        return false;
    }

    /// <summary>
    /// Authorize permission
    /// </summary>
    /// <param name="permissionRecordSystemName">Permission record system name</param>
    /// <param name="customerRoleId">Customer role identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the true - authorized; otherwise, false
    /// </returns>
    public async Task<bool> AuthorizeAsync(string permissionRecordSystemName, int customerRoleId)
    {
        if (string.IsNullOrEmpty(permissionRecordSystemName))
            return false;

        var key = _staticCacheManager.PrepareKeyForDefaultCache(NopSecurityDefaults.PermissionAllowedCacheKey, permissionRecordSystemName, customerRoleId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var permissions = await GetPermissionRecordsByCustomerRoleIdAsync(customerRoleId);
            foreach (var permission in permissions)
                if (permission.SystemName.Equals(permissionRecordSystemName, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        });
    }

    #endregion
}