using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nop.Plugin.Widgets.ImagePuzzle.Components;
using Nop.Services.Cms;
using Nop.Services.Plugins;
using Nop.Services.Configuration;
using Nop.Web.Framework.Infrastructure;

namespace Nop.Plugin.Widgets.ImagePuzzle;

public partial class ImagePuzzlePlugin : BasePlugin, IWidgetPlugin
{
    private readonly ISettingService _settingService;

    public ImagePuzzlePlugin(ISettingService settingService)
    {
        _settingService = settingService;
    }

    public bool HideInWidgetList => false;

    public Task<IList<string>> GetWidgetZonesAsync()
    {
        return Task.FromResult<IList<string>>(new List<string>
        {
            PublicWidgetZones.ProductDetailsBeforePictures
        });
    }

    public Type GetWidgetViewComponent(string widgetZone)
    {
        return typeof(PuzzleWidgetViewComponent);
    }

    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new PuzzleSettings
        {
            GridSize = 3
        });

        var widgetSettings = await _settingService.LoadSettingAsync<Nop.Core.Domain.Cms.WidgetSettings>();
        if (!widgetSettings.ActiveWidgetSystemNames.Contains("Widgets.ImagePuzzle"))
        {
            widgetSettings.ActiveWidgetSystemNames.Add("Widgets.ImagePuzzle");
            await _settingService.SaveSettingAsync(widgetSettings);
        }

        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<PuzzleSettings>();

        var widgetSettings = await _settingService.LoadSettingAsync<Nop.Core.Domain.Cms.WidgetSettings>();
        if (widgetSettings.ActiveWidgetSystemNames.Contains("Widgets.ImagePuzzle"))
        {
            widgetSettings.ActiveWidgetSystemNames.Remove("Widgets.ImagePuzzle");
            await _settingService.SaveSettingAsync(widgetSettings);
        }

        await base.UninstallAsync();
    }
}