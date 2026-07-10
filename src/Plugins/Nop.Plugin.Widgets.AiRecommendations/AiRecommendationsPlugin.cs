using Nop.Core;
using Nop.Plugin.Widgets.AiRecommendations.Tasks;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Helpers;
using Nop.Web.Framework.Infrastructure;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Services.Localization;

namespace Nop.Plugin.Widgets.AiRecommendations;

public class AiRecommendationsPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly IScheduleTaskService _scheduleTaskService;
    private readonly IWebHelper _webHelper;
    private readonly ILocalizationService _localizationService;

    public bool HideInWidgetList => false;

    public AiRecommendationsPlugin(
        ISettingService settingService,
        IScheduleTaskService scheduleTaskService,
        IWebHelper webHelper,
        ILocalizationService localizationService)
    {
        _settingService = settingService;
        _scheduleTaskService = scheduleTaskService;
        _webHelper = webHelper;
        _localizationService = localizationService;
    }

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            // Product page — similar products
            PublicWidgetZones.ProductDetailsBottom,

            // Homepage — personalised recommendations
            PublicWidgetZones.HomepageBottom,

            // Cart — complete your order
            PublicWidgetZones.OrderSummaryContentBefore
        });
    }

    public Type? GetWidgetViewComponent(string widgetZone)
    {
        return widgetZone == PublicWidgetZones.ProductDetailsBottom
            ? typeof(Components.SimilarProductsViewComponent)
            : typeof(Components.PersonalisedRecommendationsViewComponent);
    }

    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/AiRecommendations/Configure";

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new AiRecommendationsSettings
        {
            Enabled = false,
            EmbeddingDeploymentName = "text-embedding-ada-002",
            RecommendationCount = 4,
            MinSimilarityScore = 0.75,
            ShowOnHomepage = true,
            ShowOnProductPage = true,
            ShowOnCart = true,
            UseAzureKeyVault = false,
            AzureKeyVaultUrl = string.Empty,
            AzureKeyVaultSecretName = string.Empty
        });

        var task = await _scheduleTaskService.GetTaskByTypeAsync(
            typeof(GenerateEmbeddingsTask).FullName!);

        if (task == null)
        {
            await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            {
                Name = "AI Recommendations — Generate Embeddings",
                Seconds = 86400, // nightly
                Type = typeof(GenerateEmbeddingsTask).FullName!,
                Enabled = false,
                StopOnError = false
            });
        }

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Widgets.AiRecommendations.Enabled"] = "Enable AI Recommendations",
            ["Plugins.Widgets.AiRecommendations.RecommendationCount"] = "Recommendation count",
            ["Plugins.Widgets.AiRecommendations.MinSimilarityScore"] = "Minimum similarity score",
            ["Plugins.Widgets.AiRecommendations.ShowOnProductPage"] = "Show on product page",
            ["Plugins.Widgets.AiRecommendations.ShowOnHomepage"] = "Show on homepage",
            ["Plugins.Widgets.AiRecommendations.ShowOnCart"] = "Show on cart page",
            ["Plugins.Widgets.AiRecommendations.AzureOpenAIEndpoint"] = "Azure OpenAI Endpoint",
            ["Plugins.Widgets.AiRecommendations.AzureOpenAIApiKey"] = "Azure OpenAI API Key",
            ["Plugins.Widgets.AiRecommendations.EmbeddingDeploymentName"] = "Embedding deployment name",
            ["Plugins.Widgets.AiRecommendations.UseAzureKeyVault"] = "Use Azure Key Vault",
            ["Plugins.Widgets.AiRecommendations.AzureKeyVaultUrl"] = "Azure Key Vault URL",
            ["Plugins.Widgets.AiRecommendations.AzureKeyVaultSecretName"] = "Azure Key Vault Secret Name"
        });

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<AiRecommendationsSettings>();

        var task = await _scheduleTaskService.GetTaskByTypeAsync(
            typeof(GenerateEmbeddingsTask).FullName!);

        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.AiRecommendations");

        await base.UninstallAsync();
    }
}
