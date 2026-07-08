using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Widgets.AiRecommendations.Models;
using Nop.Plugin.Widgets.AiRecommendations.Services;
using Nop.Plugin.Widgets.AiRecommendations.Tasks;
using Nop.Services.Configuration;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Widgets.AiRecommendations.Controllers;

[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class AiRecommendationsController : BasePluginController
{
    private readonly AiRecommendationsSettings _settings;
    private readonly ISettingService _settingService;
    private readonly INotificationService _notificationService;
    private readonly EmbeddingService _embeddingService;

    public AiRecommendationsController(
        AiRecommendationsSettings settings,
        ISettingService settingService,
        INotificationService notificationService,
        EmbeddingService embeddingService)
    {
        _settings = settings;
        _settingService = settingService;
        _notificationService = notificationService;
        _embeddingService = embeddingService;
    }

    public IActionResult Configure()
    {
        var model = new ConfigurationModel
        {
            Enabled = _settings.Enabled,
            AzureOpenAIEndpoint = _settings.AzureOpenAIEndpoint,
            AzureOpenAIApiKey = _settings.AzureOpenAIApiKey,
            EmbeddingDeploymentName = _settings.EmbeddingDeploymentName,
            RecommendationCount = _settings.RecommendationCount,
            MinSimilarityScore = _settings.MinSimilarityScore,
            ShowOnHomepage = _settings.ShowOnHomepage,
            ShowOnProductPage = _settings.ShowOnProductPage,
            ShowOnCart = _settings.ShowOnCart,
            UseAzureKeyVault = _settings.UseAzureKeyVault,
            AzureKeyVaultUrl = _settings.AzureKeyVaultUrl,
            AzureKeyVaultSecretName = _settings.AzureKeyVaultSecretName
        };

        return View("~/Plugins/Widgets.AiRecommendations/Views/Configure.cshtml", model);
    }

    [HttpPost]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/Widgets.AiRecommendations/Views/Configure.cshtml", model);

        _settings.Enabled = model.Enabled;
        _settings.AzureOpenAIEndpoint = model.AzureOpenAIEndpoint?.Trim() ?? string.Empty;
        _settings.AzureOpenAIApiKey = model.AzureOpenAIApiKey?.Trim() ?? string.Empty;
        _settings.EmbeddingDeploymentName = model.EmbeddingDeploymentName?.Trim() ?? string.Empty;
        _settings.RecommendationCount = model.RecommendationCount;
        _settings.MinSimilarityScore = model.MinSimilarityScore;
        _settings.ShowOnHomepage = model.ShowOnHomepage;
        _settings.ShowOnProductPage = model.ShowOnProductPage;
        _settings.ShowOnCart = model.ShowOnCart;
        _settings.UseAzureKeyVault = model.UseAzureKeyVault;
        _settings.AzureKeyVaultUrl = model.AzureKeyVaultUrl?.Trim() ?? string.Empty;
        _settings.AzureKeyVaultSecretName = model.AzureKeyVaultSecretName?.Trim() ?? string.Empty;

        await _settingService.SaveSettingAsync(_settings);
        _notificationService.SuccessNotification("AI Recommendations settings saved.");

        return RedirectToAction("Configure");
    }

    /// <summary>
    /// Manually trigger embedding generation from the admin UI
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GenerateEmbeddings()
    {
        await _embeddingService.GenerateAllEmbeddingsAsync();
        _notificationService.SuccessNotification("Embedding generation complete — recommendations are ready.");
        return RedirectToAction("Configure");
    }
}
