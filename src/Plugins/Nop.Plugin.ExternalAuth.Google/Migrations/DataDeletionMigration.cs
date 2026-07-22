using FluentMigrator;
using Nop.Data;
using Nop.Data.Migrations;
using Nop.Web.Framework.Extensions;

namespace Nop.Plugin.ExternalAuth.Google.Migrations;

[NopMigration("2022-06-23 00:00:00", "ExternalAuth.Google 1.77. Data deletion feature", MigrationProcessType.Update)]
public class DataDeletionMigration : MigrationBase
{
    #region Methods

    /// <summary>
    /// Collect the UP migration expressions
    /// </summary>
    public override void Up()
    {
        if (!DataSettingsManager.IsDatabaseInstalled())
            return;

        this.AddOrUpdateLocaleResource(new Dictionary<string, string>
        {
            ["Plugins.ExternalAuth.Google.AuthenticationDataDeletedSuccessfully"] = "Data deletion request completed",
            ["Plugins.ExternalAuth.Google.AuthenticationDataExist"] = "Data deletion request is pending, please contact the admin",
            ["Plugins.ExternalAuth.Google.Instructions"] = "<p>To configure authentication with Google, please follow these steps:<br/><br/><ol><li>Navigate to the <a href=\"https://developers.Google.com/apps\" target =\"_blank\"> Google for Developers</a> page and sign in. If you don't already have a Google account, use the <b>Sign up for Google</b> link on the login page to create one.</li><li>Tap the <b>+ Add a New App button</b> in the upper right corner to create a new App ID. (If this is your first app with Google, the text of the button will be <b>Create a New App</b>.)</li><li>Fill out the form and tap the <b>Create App ID button</b>.</li><li>The <b>Product Setup</b> page is displayed, letting you select the features for your new app. Click <b>Get Started</b> on <b>Google Login</b>.</li><li>Click the <b>Settings</b> link in the menu at the left, you are presented with the <b>Client OAuth Settings</b> page with some defaults already set.</li><li>Enter \"{0:s}signin-Google\" into the <b>Valid OAuth Redirect URIs</b> field.</li><li>From User data deletion dropdown menu select \"Data deletion instructions URL\" </li><li> Enter \"{0:s}Google/data-deletion-callback/\" into the <b> User data deletion </b> input field.</li><li>Click <b>Save Changes</b>.</li><li>Click the <b>Dashboard</b> link in the left navigation.</li><li>Copy your App ID and App secret below.</li></ol><br/><br/></p>"
        });
    }

    /// <summary>
    /// Collects the DOWN migration expressions
    /// </summary>
    public override void Down()
    {
        //nothing
    }

    #endregion
}