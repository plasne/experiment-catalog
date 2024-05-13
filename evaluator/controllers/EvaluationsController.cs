using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

[ApiController]
[Route("api/evaluations")]
public class EvaluationsController() : ControllerBase
{
    [HttpPost("start")]
    public async Task<IActionResult> Start(
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] EnqueueRequest request)
    {
        await serviceProvider
            .GetServices<IHostedService>()
            .OfType<AzureStorageQueueWriter>()
            .First()
            .StartEnqueueRequestAsync(request);
        return this.Created();
    }

    // TODO: need to add a way to get status
}
