using Nop.Core;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Payments;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.Payments.Square.Services;

/// <summary>
/// Represents a schedule task to renew the access token
/// </summary>
public class RenewAccessTokenTask : IScheduleTask
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly ILogger _logger;
    private readonly IPaymentPluginManager _paymentPluginManager;
    private readonly ISettingService _settingService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    private readonly SquarePaymentManager _squarePaymentManager;
    private readonly SquarePaymentSettings _squarePaymentSettings;

    #endregion

    #region Ctor

    public RenewAccessTokenTask(ILocalizationService localizationService,
        ILogger logger,
        IPaymentPluginManager paymentPluginManager,
        ISettingService settingService,
        IStoreContext storeContext,
        IStoreService storeService,
        SquarePaymentManager squarePaymentManager,
        SquarePaymentSettings squarePaymentSettings)
    {
        _localizationService = localizationService;
        _logger = logger;
        _paymentPluginManager = paymentPluginManager;
        _settingService = settingService;
        _storeContext = storeContext;
        _storeService = storeService;
        _squarePaymentManager = squarePaymentManager;
        _squarePaymentSettings = squarePaymentSettings;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Executes a task
    /// </summary>
    /// <returns>A task that represents the asynchronous operation</returns>
    public async Task ExecuteAsync()
    {
        //whether plugin is active
        if (!await _paymentPluginManager.IsPluginActiveAsync(SquarePaymentDefaults.SystemName))
            return;

        //do not execute for sandbox environment
        if (_squarePaymentSettings.UseSandbox)
            return;

        try
        {
            var allStores = await _storeService.GetAllStoresAsync();
            var currentStore = await _storeContext.GetCurrentStoreAsync();

            // Collect all potential store scopes: Store 0 (shared / all stores) and each distinct store ID
            var storeIds = new HashSet<int> { 0 };
            foreach (var store in allStores)
                storeIds.Add(store.Id);
            if (currentStore != null)
                storeIds.Add(currentStore.Id);

            // Group store scopes by distinct RefreshToken to renew each token only once
            // (Calling Square repeatedly with the same refresh token will fail because each refresh invalidates the previous token)
            var tokenGroups = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sId in storeIds)
            {
                var s = await _settingService.LoadSettingAsync<SquarePaymentSettings>(sId);
                if (!s.UseSandbox && !string.IsNullOrEmpty(s.RefreshToken) && !string.IsNullOrEmpty(s.ApplicationSecret))
                {
                    if (!tokenGroups.TryGetValue(s.RefreshToken, out var list))
                    {
                        list = new List<int>();
                        tokenGroups[s.RefreshToken] = list;
                    }
                    list.Add(sId);
                }
            }

            // Fallback if no specific refresh tokens found in storeIds, try injected settings
            if (!tokenGroups.Any())
            {
                if (!string.IsNullOrEmpty(_squarePaymentSettings.RefreshToken))
                    tokenGroups[_squarePaymentSettings.RefreshToken] = storeIds.ToList();
                else
                {
                    await _logger.WarningAsync("Square payment: No refresh token found to renew.");
                    return;
                }
            }

            var renewedCount = 0;

            foreach (var group in tokenGroups)
            {
                var targetStoreId = group.Value.First();

                // Get the new access and refresh tokens from Square
                var (newAccessToken, refreshToken) = await _squarePaymentManager.RenewAccessTokenAsync(targetStoreId);
                if (string.IsNullOrEmpty(newAccessToken) || string.IsNullOrEmpty(refreshToken))
                    throw new NopException($"No service response received when renewing Square access token for store ID {targetStoreId}");

                // Save to all store scopes that share this token
                foreach (var sId in group.Value)
                {
                    var s = await _settingService.LoadSettingAsync<SquarePaymentSettings>(sId);
                    s.AccessToken = newAccessToken;
                    s.RefreshToken = refreshToken;

                    await _settingService.SaveSettingAsync(s, x => x.AccessToken, sId, false);
                    await _settingService.SaveSettingAsync(s, x => x.RefreshToken, sId, false);
                }

                // Ensure store 0 is updated as well so Admin Configure always has the current token
                if (!group.Value.Contains(0))
                {
                    var s0 = await _settingService.LoadSettingAsync<SquarePaymentSettings>(0);
                    s0.AccessToken = newAccessToken;
                    s0.RefreshToken = refreshToken;

                    await _settingService.SaveSettingAsync(s0, x => x.AccessToken, 0, false);
                    await _settingService.SaveSettingAsync(s0, x => x.RefreshToken, 0, false);
                }

                _squarePaymentSettings.AccessToken = newAccessToken;
                _squarePaymentSettings.RefreshToken = refreshToken;

                renewedCount++;
            }

            if (renewedCount > 0)
            {
                await _settingService.ClearCacheAsync();

                // Log information about the successful renew of the access token
                await _logger.InformationAsync(await _localizationService.GetResourceAsync("Plugins.Payments.Square.RenewAccessToken.Success"));
            }
        }
        catch (Exception exception)
        {
            // Log error on renewing of the access token
            await _logger.ErrorAsync(await _localizationService.GetResourceAsync("Plugins.Payments.Square.RenewAccessToken.Error"), exception);
            throw; // Re-throw so ScheduleTaskRunner accurately records failure instead of marking success
        }
    }

    #endregion
}
