using Nop.Core;
using Nop.Services.Cms;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Helpers;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.SeoEnhancements;

public class SeoEnhancementsPlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;
    private readonly ILocalizationService _localizationService;
    private readonly IWebHelper _webHelper;

    public bool HideInWidgetList => false;

    public SeoEnhancementsPlugin(
        ISettingService settingService,
        ILocalizationService localizationService,
        IWebHelper webHelper)
    {
        _settingService = settingService;
        _localizationService = localizationService;
        _webHelper = webHelper;
    }

    /// <summary>
    /// Widget zones this plugin injects into.
    /// - head_html_tag: JSON-LD schema scripts on product/category pages
    /// - productdetails_top: FAQ accordion on product pages
    /// - categorydetails_top: FAQ accordion on category pages
    /// </summary>
    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.HeadHtmlTag,
            PublicWidgetZones.ProductDetailsTop,
            "categorydetails_top"  // nopCommerce 4.80+ standard zone
        });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        if (widgetZone.Equals(PublicWidgetZones.HeadHtmlTag, StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoSchemaViewComponent);
        if (widgetZone.Equals(PublicWidgetZones.ProductDetailsTop, StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoFaqViewComponent);
        if (widgetZone.Equals("categorydetails_top", StringComparison.InvariantCultureIgnoreCase))
            return typeof(Components.SeoFaqViewComponent);

        return null;
    }

    public override string GetConfigurationPageUrl()
    {
        return $"{_webHelper.GetStoreLocation()}Admin/SeoEnhancements/Configure";
    }

    public override async Task InstallAsync()
    {
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

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.Widgets.SeoEnhancements");
        await base.UninstallAsync();
    }
}
