using Microsoft.AspNetCore.Authentication;
using AspNet.Security.OAuth.Apple;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Services.Authentication.External;

namespace Nop.Plugin.ExternalAuth.Apple.Infrastructure;

/// <summary>
/// Represents registrar of Apple authentication service
/// </summary>
public class AppleAuthenticationRegistrar : IExternalAuthenticationRegistrar
{
    /// <summary>
    /// Configure
    /// </summary>
    /// <param name="builder">Authentication builder</param>
    public void Configure(AuthenticationBuilder builder)
    {
        builder.AddApple("Apple", options =>
        {
            //set credentials
            var settings = EngineContext.Current.Resolve<AppleExternalAuthSettings>();

            options.ClientId = string.IsNullOrEmpty(settings?.ClientId) ? nameof(options.ClientId) : settings.ClientId;
            options.TeamId = string.IsNullOrEmpty(settings?.TeamId) ? nameof(options.TeamId) : settings.TeamId;
            options.KeyId = string.IsNullOrEmpty(settings?.KeyId) ? nameof(options.KeyId) : settings.KeyId;
            options.PrivateKey = (keyId, cancellationToken) => Task.FromResult<ReadOnlyMemory<char>>(settings?.PrivateKey?.AsMemory() ?? new ReadOnlyMemory<char>());

            //store access and refresh tokens for the further usage
            options.SaveTokens = true;

            //set custom events handlers
            options.Events = new AppleAuthenticationEvents
            {
                //in case of error, redirect the user to the specified URL
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();

                    var errorUrl = context.Properties.GetString(AppleAuthenticationDefaults.ErrorCallback);
                    context.Response.Redirect(errorUrl);

                    return Task.FromResult(0);
                }
            };
        });
    }
}