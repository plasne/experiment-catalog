using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Evaluator;

[ApiController]
[Route("api")]
public class EvaluationsController() : ControllerBase
{
    [HttpPost("jobs")]
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

    [HttpGet("queue-depth")]
    public async Task<ActionResult<Dictionary<string, int>>> Status(
        [FromServices] IServiceProvider serviceProvider
    )
    {
        var status = new Dictionary<string, int>();
        var inferenceReader = serviceProvider
            .GetServices<IHostedService>()
            .OfType<AzureStorageQueueReaderForInference>()
            .FirstOrDefault();
        if (inferenceReader is not null)
        {
            var inferenceStatus = await inferenceReader.GetAllQueueMessageCountsAsync();
            foreach (var kvp in inferenceStatus)
            {
                status[kvp.Key] = kvp.Value;
            }
        }
        var evaluationReader = serviceProvider
            .GetServices<IHostedService>()
            .OfType<AzureStorageQueueReaderForEvaluation>()
            .FirstOrDefault();
        if (evaluationReader is not null)
        {
            var evaluationStatus = await evaluationReader.GetAllQueueMessageCountsAsync();
            foreach (var kvp in evaluationStatus)
            {
                status[kvp.Key] = kvp.Value;
            }
        }
        return this.Ok(status);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobSummary>>> GetJobs(
        [FromServices] JobStatusService jobStatusService,
        [FromQuery] int? ago,
        CancellationToken cancellationToken)
    {
        ago ??= 3;
        var since = DateTimeOffset.UtcNow.AddDays(-ago.Value);
        var jobs = await jobStatusService.ListJobsAsync(since, cancellationToken);
        return this.Ok(jobs);
    }

    [HttpGet("jobs/{runId}/status")]
    public async Task<ActionResult<JobStatus>> GetJobStatus(
        [FromServices] JobStatusService jobStatusService,
        [FromRoute] string runId,
        CancellationToken cancellationToken)
    {
        var status = await jobStatusService.GetJobStatusAsync(runId, cancellationToken);
        if (status is null)
        {
            return this.StatusCode(503, "Job status tracking is not enabled.");
        }

        return this.Ok(status);
    }
}
