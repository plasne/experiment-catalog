// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Evaluator;

[ApiController]
[Route("api/evaluations")]
public class EvaluationsController() : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<EnqueueResponse>> Start(
        [FromServices] IServiceProvider serviceProvider,
        [FromBody] EnqueueRequest request)
    {
        await serviceProvider
            .GetServices<IHostedService>()
            .OfType<AzureStorageQueueWriter>()
            .First()
            .StartEnqueueRequestAsync(request);
        return this.Created(null as Uri, new EnqueueResponse { RunId = request.RunId });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        throw new HttpException(501, "not implemented");
    }
}
