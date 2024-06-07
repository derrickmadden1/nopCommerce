using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Security;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Areas.Admin.Models.Security;
using Nop.Web.Framework.Models.Extensions;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Web.Areas.Admin.Controllers;

public partial class SecurityController : BaseAdminController
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly Services.Logging.ILogger _logger;
    protected readonly IPermissionManager _permissionManager;
    protected readonly INotificationService _notificationService;
    protected readonly IPermissionService _permissionService;
    protected readonly ISettingService _settingService;
    protected readonly IWorkContext _workContext;
    protected readonly SecuritySettings _securitySettings;

    private static readonly char[] _separator = [','];

    #endregion

    #region Ctor

    public SecurityController(ICustomerService customerService,
        ILocalizationService localizationService,
        Services.Logging.ILogger logger,
        IPermissionManager permissionManager,
        INotificationService notificationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IWorkContext workContext,
        SecuritySettings securitySettings)
    {
        _customerService = customerService;
        _localizationService = localizationService;
        _logger = logger;
        _permissionManager = permissionManager;
        _notificationService = notificationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _workContext = workContext;
        _securitySettings = securitySettings;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Prepare permission item model
    /// </summary>
    /// <param name="permissionRecord">Permission record</param>
    /// <param name="availableRoles">All available customer roles</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permission item model
    /// </returns>
    protected virtual async Task<PermissionItemModel> PreparePermissionItemModelAsync(PermissionRecord permissionRecord, IList<CustomerRole> availableRoles = null)
    {
        availableRoles ??= await _customerService.GetAllCustomerRolesAsync(showHidden: true);

        var mapping = await _permissionManager.GetMappingByPermissionRecordIdAsync(permissionRecord.Id);

        var names = await mapping
            .Select(m => availableRoles.FirstOrDefault(p => p.Id == m.CustomerRoleId))
            .Where(r => r != null).Select(r => r.Name).ToListAsync();

        var (ids, appliedFor) = (mapping.Select(m => m.CustomerRoleId).ToList(), string.Join(", ", names));

        //fill in model values from the entity
        var permissionItemModel = new PermissionItemModel
        {
            Id = permissionRecord.Id,
            PermissionName = await _localizationService.GetLocalizedPermissionNameAsync(permissionRecord, await _workContext.GetWorkingLanguageAsync()),
            PermissionAppliedFor = appliedFor,
            SelectedCustomerRoleIds = ids.ToList(),
            AvailableCustomerRoles = availableRoles.Select(role => new SelectListItem
            {
                Text = role.Name,
                Value = role.Id.ToString(),
                Selected = ids.Contains(role.Id)
            }).ToList()
        };

        return permissionItemModel;
    }

    /// <summary>
    /// Prepare permission category list model
    /// </summary>
    /// <param name="searchModel">permission category search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permission category list model
    /// </returns>
    protected async Task<PermissionCategoryListModel> PreparePermissionCategoryListModelAsync(PermissionCategorySearchModel searchModel)
    {
        var permissions = await _permissionManager.GetAllPermissionRecordsAsync();

        var types = permissions
            .GroupBy(p => p.Category, p => p)
            .Select(p => p.Key).ToList();

        var pagedTypes = types.ToPagedList(searchModel);

        //prepare list model
        var model = new PermissionCategoryListModel().PrepareToGrid(searchModel, pagedTypes, () =>
        {
            //fill in model values from the entity
            return pagedTypes.Select(t => new PermissionCategoryModel
            {
                Name = t
            });
        });

        return model;
    }

    /// <summary>
    /// Prepare ACL configuration model
    /// </summary>
    /// <param name="model">Configuration model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the configuration model
    /// </returns>
    protected async Task<ConfigurationModel> PrepareConfigurationModelAsync(ConfigurationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var customerRoles = await _customerService.GetAllCustomerRolesAsync(true);
        model.AreCustomerRolesAvailable = customerRoles.Any();
        var permissionRecords = await _permissionManager.GetAllPermissionRecordsAsync();
        model.IsPermissionsAvailable = permissionRecords.Any();

        return model;
    }

    /// <summary>
    /// Prepare paged permission item list model
    /// </summary>
    /// <param name="searchModel">Permission item search model</param>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the permission item list model
    /// </returns>
    protected virtual async Task<PermissionItemListModel> PreparePermissionItemListModelAsync(PermissionItemSearchModel searchModel)
    {
        if (searchModel == null)
            throw new ArgumentNullException(nameof(searchModel));

        //get permissions
        var permissionItems = (await _permissionManager.GetAllPermissionRecordsAsync()).Where(p => p.Category == searchModel.PermissionCategoryName).ToList().ToPagedList(searchModel);

        var availableRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);

        //prepare list model
        var model = await new PermissionItemListModel().PrepareToGridAsync(searchModel, permissionItems, () =>
        {
            //fill in model values from the entity
            return permissionItems.SelectAwait(async item => await PreparePermissionItemModelAsync(item, availableRoles));
        });

        return model;
    }

    #endregion

    #region Methods

    public virtual async Task<IActionResult> AccessDenied(string pageUrl)
    {
        var currentCustomer = await _workContext.GetCurrentCustomerAsync();
        if (currentCustomer == null || await _customerService.IsGuestAsync(currentCustomer))
        {
            await _logger.InformationAsync($"Access denied to anonymous request on {pageUrl}");
            return View();
        }

        await _logger.InformationAsync($"Access denied to user #{currentCustomer.Email} '{currentCustomer.Email}' on {pageUrl}");

        return View();
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public virtual async Task<IActionResult> PermissionCategory(PermissionItemSearchModel searchModel)
    {
        var model = await PreparePermissionItemListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public virtual async Task<IActionResult> PermissionEditPopup(int id)
    {
        var permissionRecord = await _permissionManager.GetPermissionRecordByIdAsync(id);

        return View(await PreparePermissionItemModelAsync(permissionRecord));
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public virtual async Task<IActionResult> PermissionEditPopup(PermissionItemModel model)
    {
        if (ModelState.IsValid)
        {
            var mapping = await _permissionManager.GetMappingByPermissionRecordIdAsync(model.Id);

            var rolesForDelete = mapping.Where(p => !model.SelectedCustomerRoleIds.Contains(p.CustomerRoleId))
                .Select(p => p.CustomerRoleId);

            var rolesToAdd = model.SelectedCustomerRoleIds.Where(p => mapping.All(m => m.CustomerRoleId != p));

            foreach (var customerRoleId in rolesForDelete)
                await _permissionManager.DeletePermissionRecordCustomerRoleMappingAsync(model.Id, customerRoleId);

            foreach (var customerRoleId in rolesToAdd)
                await _permissionManager.InsertPermissionRecordCustomerRoleMappingAsync(new PermissionRecordCustomerRoleMapping
                {
                    PermissionRecordId = model.Id,
                    CustomerRoleId = customerRoleId
                });
            ViewBag.RefreshPage = true;

            var permissionRecord = await _permissionManager.GetPermissionRecordByIdAsync(model.Id);
            model = await PreparePermissionItemModelAsync(permissionRecord);

            return View(model);
        }

        //if we got this far, something failed, redisplay form
        return View(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public virtual async Task<IActionResult> PermissionCategories(PermissionCategorySearchModel searchModel)
    {
        var model = await PreparePermissionCategoryListModelAsync(searchModel);

        return Json(model);
    }

    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public async Task<IActionResult> Permissions()
    {
        //prepare model
        var model = await PrepareConfigurationModelAsync(new ConfigurationModel());

        return View(model);
    }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_ACL)]
    public async Task<IActionResult> Permissions(ConfigurationModel model, IFormCollection form)
    {
        await _settingService.SaveSettingAsync(_securitySettings);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Configuration.ACL.Updated"));

        return await Permissions();
    }

    #endregion
}