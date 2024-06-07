using FluentMigrator;
using Nop.Core.Configuration;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Configuration;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Services.Configuration;

namespace Nop.Web.Framework.Migrations.UpgradeTo480;

[NopUpdateMigration("2023-05-21 12:00:00", "4.80", UpdateMigrationType.Settings)]
public class SettingMigration : MigrationBase
{
    /// <summary>Collect the UP migration expressions</summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        //do not use DI, because it produces exception on the installation process
        var settingService = EngineContext.Current.Resolve<ISettingService>();

        var allSettings = settingService.GetAllSettings();
        var settingsToDelete = new List<Setting>();

        //#7215
        var displayAttributeCombinationImagesOnly = settingService.GetSetting("producteditorsettings.displayattributecombinationimagesonly");
        if (displayAttributeCombinationImagesOnly is not null)
            settingsToDelete.Add(displayAttributeCombinationImagesOnly);
        
        //ACL settings
        settingsToDelete.AddRange(allSettings.Where(setting => setting.Name.Equals("catalogsettings.ignoreacl", StringComparison.InvariantCultureIgnoreCase)));

        settingService.DeleteSettings(settingsToDelete);
    }

    public override void Down()
    {
        //add the downgrade logic if necessary 
    }
}
