using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetBricks;

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
        [FromServices] LogsQueryClient logsQueryClient,
        [FromServices] IConfigFactory<IConfig> configFactory,
        [FromQuery] DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var config = await configFactory.GetAsync(cancellationToken);
        if (string.IsNullOrEmpty(config.LOG_ANALYTICS_WORKSPACE_ID))
        {
            throw new HttpException(500, "LOG_ANALYTICS_WORKSPACE_ID is not configured.");
        }

        var sinceValue = since ?? DateTimeOffset.UtcNow.AddDays(-7);
        var kql = @"
            AppTraces
            | where Properties.event_name == 'job.started'
            | project StartedAt = TimeGenerated,
                    RunId = tostring(Properties.run_id),
                    Project = tostring(Properties['project']),
                    Experiment = tostring(Properties.experiment),
                    Set = tostring(Properties['set']),
                    TotalItems = toint(Properties.total_items)
            | order by StartedAt desc";

        var response = await logsQueryClient.QueryWorkspaceAsync(
            config.LOG_ANALYTICS_WORKSPACE_ID,
            kql,
            new QueryTimeRange(sinceValue, DateTimeOffset.UtcNow),
            cancellationToken: cancellationToken);

        var jobs = new List<JobSummary>();
        foreach (var row in response.Value.Table.Rows)
        {
            jobs.Add(new JobSummary
            {
                StartedAt = row.GetDateTimeOffset("StartedAt") ?? DateTimeOffset.MinValue,
                RunId = row.GetString("RunId") ?? string.Empty,
                Project = row.GetString("Project") ?? string.Empty,
                Experiment = row.GetString("Experiment") ?? string.Empty,
                Set = row.GetString("Set") ?? string.Empty,
                TotalItems = row.GetInt32("TotalItems") ?? 0,
            });
        }

        return this.Ok(jobs);
    }

    [HttpGet("jobs/{runId}/status")]
    public async Task<ActionResult<JobStatus>> GetJobStatus(
        [FromServices] LogsQueryClient logsQueryClient,
        [FromServices] IConfigFactory<IConfig> configFactory,
        [FromRoute] string runId,
        CancellationToken cancellationToken)
    {
        var config = await configFactory.GetAsync(cancellationToken);
        if (string.IsNullOrEmpty(config.LOG_ANALYTICS_WORKSPACE_ID))
        {
            throw new HttpException(500, "LOG_ANALYTICS_WORKSPACE_ID is not configured.");
        }

        // query for total_items from job.started
        var jobKql = @"
            AppTraces
            | where Properties.event_name == 'job.started'
            | where Properties.run_id == '" + runId + @"'
            | project TotalItems = toint(Properties.total_items)
            | take 1";

        var jobResponse = await logsQueryClient.QueryWorkspaceAsync(
            config.LOG_ANALYTICS_WORKSPACE_ID,
            jobKql,
            QueryTimeRange.All,
            cancellationToken: cancellationToken);

        var totalItems = 0;
        if (jobResponse.Value.Table.Rows.Count > 0)
        {
            totalItems = jobResponse.Value.Table.Rows[0].GetInt32("TotalItems") ?? 0;
        }

        // query for work item status by stage
        var statusKql = @"
            AppTraces
            | where Properties.run_id == '" + runId + @"'
            | where Properties.event_name in ('workitem.succeeded', 'workitem.failed')
            | extend Stage = tostring(Properties.stage)
            | summarize arg_max(TimeGenerated, event_name = tostring(Properties.event_name)) by Id = tostring(Properties.id), Stage
            | summarize Succeeded = countif(event_name == 'workitem.succeeded'),
                        Failed    = countif(event_name == 'workitem.failed')
                        by Stage";

        var statusResponse = await logsQueryClient.QueryWorkspaceAsync(
            config.LOG_ANALYTICS_WORKSPACE_ID,
            statusKql,
            QueryTimeRange.All,
            cancellationToken: cancellationToken);

        var stages = new List<JobStageStatus>();
        foreach (var row in statusResponse.Value.Table.Rows)
        {
            stages.Add(new JobStageStatus
            {
                Stage = row.GetString("Stage") ?? string.Empty,
                Succeeded = row.GetInt32("Succeeded") ?? 0,
                Failed = row.GetInt32("Failed") ?? 0,
            });
        }

        return this.Ok(new JobStatus
        {
            RunId = runId,
            TotalItems = totalItems,
            Stages = stages,
        });
    }
}
