using Nop.Core.Caching;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Security;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Services.Customers;
using Nop.Services.Localization;

namespace Nop.Services.Security;

/// <summary>
/// Represents the permission manager
/// </summary>
public partial class PermissionManager : IPermissionManager
{
    #region Fields

    protected readonly ILanguageService _languageService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IRepository<CustomerRole> _customerRoleRepository;
    protected readonly IRepository<PermissionRecord> _permissionRecordRepository;
    protected readonly IRepository<PermissionRecordCustomerRoleMapping> _permissionRecordCustomerRoleMappingRepository;
    protected readonly IStaticCacheManager _staticCacheManager;

    #endregion

    #region Ctor

    public PermissionManager(ILanguageService languageService,
        ILocalizationService localizationService,
        IRepository<CustomerRole> customerRoleRepository,
        IRepository<PermissionRecord> permissionRecordRepository,
        IRepository<PermissionRecordCustomerRoleMapping> permissionRecordCustomerRoleMappingRepository,
        IStaticCacheManager staticCacheManager)
    {
        _languageService = languageService;
        _localizationService = localizationService;
        _customerRoleRepository = customerRoleRepository;
        _permissionRecordRepository = permissionRecordRepository;
        _permissionRecordCustomerRoleMappingRepository = permissionRecordCustomerRoleMappingRepository;
        _staticCacheManager = staticCacheManager;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Insert permissions by list of permission configs
    /// </summary>
    /// <param name="configs">Permission configs</param>
    protected void InstallPermissions(IList<PermissionConfig> configs)
    {
        if (!configs?.Any() ?? true)
            return;

        var languages = _languageService.GetAllLanguages(true);

        foreach (var config in configs)
        {
            //new permission (install it)
            var permission = new PermissionRecord
            {
                Name = config.Name,
                SystemName = config.SystemName,
                Category = config.Category
            };

            //save new permission
            _permissionRecordRepository.Insert(permission);

            foreach (var systemRoleName in config.DefaultCustomerRoles)
            {
                var customerRole = GetCustomerRoleBySystemName(systemRoleName);

                if (customerRole == null)
                {
                    //new role (save it)
                    customerRole = new CustomerRole
                    {
                        Name = systemRoleName,
                        Active = true,
                        SystemName = systemRoleName
                    };

                    _customerRoleRepository.Insert(customerRole);
                }

                InsertPermissionRecordCustomerRoleMapping(new PermissionRecordCustomerRoleMapping { CustomerRoleId = customerRole.Id, PermissionRecordId = permission.Id });
            }

            //save localization
            _localizationService.SaveLocalizedPermissionName(languages, permission);
        }
    }

    /// <summary>
    /// Gets a customer role
    /// </summary>
    /// <param name="systemName">Customer role system name</param>
    /// <returns>
    /// The customer role
    /// </returns>
    protected CustomerRole GetCustomerRoleBySystemName(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return null;

        var key = _staticCacheManager.PrepareKeyForDefaultCache(NopCustomerServicesDefaults.CustomerRolesBySystemNameCacheKey, systemName);

        var query = from cr in _customerRoleRepository.Table
            orderby cr.Id
            where cr.SystemName == systemName
            select cr;

        var customerRole = _staticCacheManager.Get(key, () => query.FirstOrDefault());

        return customerRole;
    }

    /// <summary>
    /// Gets a permission
    /// </summary>
    /// <param name="systemName">Permission system name</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permission
    /// </returns>
    protected async Task<PermissionRecord> GetPermissionRecordBySystemNameAsync(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
            return null;

        var query = from pr in _permissionRecordRepository.Table
            where pr.SystemName == systemName
            orderby pr.Id
            select pr;

        var permissionRecord = await query.FirstOrDefaultAsync();

        return permissionRecord;
    }
    
    /// <summary>
    /// Inserts a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionRecordCustomerRoleMapping">Permission record-customer role mapping</param>
    protected void InsertPermissionRecordCustomerRoleMapping(PermissionRecordCustomerRoleMapping permissionRecordCustomerRoleMapping)
    {
        var exists = _permissionRecordCustomerRoleMappingRepository.GetAll();

        var mapping = exists.FirstOrDefault(m =>
            m.CustomerRoleId == permissionRecordCustomerRoleMapping.CustomerRoleId &&
            m.PermissionRecordId == permissionRecordCustomerRoleMapping.PermissionRecordId);

        if (mapping != null)
        {
            permissionRecordCustomerRoleMapping.Id = mapping.Id;

            return;
        }

        _permissionRecordCustomerRoleMappingRepository.Insert(permissionRecordCustomerRoleMapping);
    }

    #endregion

    #region Methods
    
    /// <summary>
    /// Gets a permission record by identifier
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains a permission record
    /// </returns>
    public virtual async Task<PermissionRecord> GetPermissionRecordByIdAsync(int permissionId)
    {
        return await _permissionRecordRepository.GetByIdAsync(permissionId);
    }

    /// <summary>
    /// Gets all permissions
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permissions
    /// </returns>
    public virtual async Task<IList<PermissionRecord>> GetAllPermissionRecordsAsync()
    {
        var permissions = await _permissionRecordRepository.GetAllAsync(query => query.OrderBy(pr => pr.Name), _ => null);

        return permissions;
    }

    /// <summary>
    /// Updates the permission
    /// </summary>
    /// <param name="permission">Permission</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task UpdatePermissionRecordAsync(PermissionRecord permission)
    {
        await _permissionRecordRepository.UpdateAsync(permission);
    }

    /// <summary>
    /// Delete a permission
    /// </summary>
    /// <param name="permissionSystemName">Permission system name</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public virtual async Task DeletePermissionAsync(string permissionSystemName)
    {
        var permission = await GetPermissionRecordBySystemNameAsync(permissionSystemName);

        if (permission == null)
            return;

        var mapping = await GetMappingByPermissionRecordIdAsync(permission.Id);

        await _permissionRecordCustomerRoleMappingRepository.DeleteAsync(mapping);
        await _localizationService.DeleteLocalizedPermissionNameAsync(await _languageService.GetAllLanguagesAsync(true), permission);
        await _permissionRecordRepository.DeleteAsync(permission);
    }

    /// <summary>
    /// Gets a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task<IList<PermissionRecordCustomerRoleMapping>> GetMappingByPermissionRecordIdAsync(int permissionId)
    {
        var query = _permissionRecordCustomerRoleMappingRepository.Table;

        query = query.Where(x => x.PermissionRecordId == permissionId);

        return await query.ToListAsync();
    }

    /// <summary>
    /// Delete a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionId">Permission identifier</param>
    /// <param name="customerRoleId">Customer role identifier</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task DeletePermissionRecordCustomerRoleMappingAsync(int permissionId, int customerRoleId)
    {
        var mapping = _permissionRecordCustomerRoleMappingRepository.Table
            .FirstOrDefault(prcm => prcm.CustomerRoleId == customerRoleId && prcm.PermissionRecordId == permissionId);
        if (mapping is null)
            return;

        await _permissionRecordCustomerRoleMappingRepository.DeleteAsync(mapping);
    }

    /// <summary>
    /// Inserts a permission record-customer role mapping
    /// </summary>
    /// <param name="permissionRecordCustomerRoleMapping">Permission record-customer role mapping</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task InsertPermissionRecordCustomerRoleMappingAsync(PermissionRecordCustomerRoleMapping permissionRecordCustomerRoleMapping)
    {
        await _permissionRecordCustomerRoleMappingRepository.InsertAsync(permissionRecordCustomerRoleMapping);
    }

    /// <summary>
    /// Configure permission manager
    /// </summary>
    public void Configure()
    {
        var permissionRecords = _permissionRecordRepository.GetAll(getCacheKey: _ => null).Distinct().ToHashSet();
        var exists = permissionRecords.Select(p => p.SystemName).ToHashSet();

        var configs = Singleton<ITypeFinder>.Instance.FindClassesOfType<IPermissionConfigManager>()
            .Select(configType => (IPermissionConfigManager)Activator.CreateInstance(configType))
            .SelectMany(config => config?.AllConfigs ?? new List<PermissionConfig>())
            .Where(c => !exists.Contains(c.SystemName))
            .ToList();

        InstallPermissions(configs);
    }

    /// <summary>
    /// Inserts a permission record-customer role mappings
    /// </summary>
    /// <param name="customerRoleId">Customer role ID</param>
    /// <param name="permissions">Permissions</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task InsertPermissionMappingAsync(int customerRoleId, params string[] permissions)
    {
        var permissionRecords = await GetAllPermissionRecordsAsync();

        foreach (var permissionSystemName in permissions)
        {
            var permission = permissionRecords.FirstOrDefault(p =>
                p.SystemName.Equals(permissionSystemName, StringComparison.CurrentCultureIgnoreCase));

            if (permission == null)
                continue;

            await InsertPermissionRecordCustomerRoleMappingAsync(
                new PermissionRecordCustomerRoleMapping
                {
                    CustomerRoleId = customerRoleId,
                    PermissionRecordId = permission.Id
                });
        }
    }

    #endregion
}