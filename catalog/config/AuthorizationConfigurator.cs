using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NetBricks;

namespace Catalog;

public class AuthorizationConfigurator(IConfigFactory<IConfig> configFactory) : IConfigureOptions<AuthorizationOptions>
{
    public void Configure(AuthorizationOptions options)
    {
        var config = configFactory.GetAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        if (config.IsAuthenticationEnabled)
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        }
    }
}