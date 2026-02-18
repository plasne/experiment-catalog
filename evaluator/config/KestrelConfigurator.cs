using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using NetBricks;

namespace Evaluator;

public class KestrelConfigurator(IConfigFactory<IConfig> configFactory) : IConfigureOptions<KestrelServerOptions>
{
    public void Configure(KestrelServerOptions options)
    {
        var config = configFactory.GetAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        if (config.ROLES.Contains(Roles.API))
        {
            options.ListenAnyIP(config.PORT);
        }
        else
        {
            options.Listen(IPAddress.Loopback, config.PORT);
        }
    }
}
