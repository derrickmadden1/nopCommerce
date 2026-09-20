using Nop.Core;
using Nop.Plugin.Widgets.SeoEnhancements.Domain;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Infrastructure;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.Widgets.SeoEnhancements;

public class SeoEnhancementsPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;
    private readonly IScheduleTaskService _scheduleTaskService;

    public bool HideInWidgetList => false;

    public SeoEnhancementsPlugin(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper,
        IScheduleTaskService scheduleTaskService)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
        _scheduleTaskService = scheduleTaskService;
    }

    /// <summary>
    /// Widget zones this plugin injects into.
    /// - head_html_tag: JSON-LD schema scripts on product/category pages
    /// - productdetails_bottom: FAQ accordion on product pages
    /// - categorydetails_bottom: FAQ accordion on category pages
    /// </summary>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.HeadHtmlTag,
            PublicWidgetZones.ProductDetailsBottom,
            "categorydetails_bottom"
        });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (widgetZone.Equals(PublicWidgetZones.HeadHtmlTag, StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoSchemaViewComponent);
        if (widgetZone.Equals(PublicWidgetZones.ProductDetailsBottom, StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoFaqViewComponent);
        if (widgetZone.Equals("categorydetails_bottom", StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoFaqViewComponent);

        return null!;
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/SeoEnhancements/Configure";
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new SeoEnhancementsSettings
        {
            AzureOpenAiApiVersion = "2024-08-01-preview",
            FaqPairsToGenerate = 5
        });

        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.Widgets.SeoEnhancements.Configure"] = "SEO Enhancements",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Question"] = "Question",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Answer"] = "Answer",
            ["Plugins.Widgets.SeoEnhancements.FAQ.DisplayOrder"] = "Display Order",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Published"] = "Published",
            ["Plugins.Widgets.SeoEnhancements.FAQ.AddNew"] = "Add FAQ",
            ["Plugins.Widgets.SeoEnhancements.FAQ.Edit"] = "Edit FAQ",
            ["Plugins.Widgets.SeoEnhancements.FAQ.BackToList"] = "Back to FAQ list",
        });

        if (await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Widgets.SeoEnhancements.Tasks.FaqToBlogPublishTask") == null)
        {
            await _scheduleTaskService.InsertTaskAsync(new Nop.Core.Domain.ScheduleTasks.ScheduleTask
            {
                Enabled = true,
                Seconds = 604800, // 7 days in seconds
                Name = "Publish weekly FAQ blog post",
                Type = "Nop.Plugin.Widgets.SeoEnhancements.Tasks.FaqToBlogPublishTask",
            });
        }

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        var task = await _scheduleTaskService.GetTaskByTypeAsync("Nop.Plugin.Widgets.SeoEnhancements.Tasks.FaqToBlogPublishTask");
        if (task != null)
            await _scheduleTaskService.DeleteTaskAsync(task);

        await _settingService.DeleteSettingAsync<SeoEnhancementsSettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.SeoEnhancements");
        await base.UninstallAsync();
    }
}
