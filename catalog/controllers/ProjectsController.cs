using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Catalog;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<Project>>> ListProjects(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        CancellationToken cancellationToken)
    {
        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        var projects = await storageService.GetProjectsAsync(cancellationToken);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<IActionResult> AddProject(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromBody] Project project,
        CancellationToken cancellationToken)
    {
        if (project is null)
        {
            return BadRequest("a project (as body) is required.");
        }

        if (string.IsNullOrEmpty(project.Name))
        {
            return BadRequest("an project name is required.");
        }

        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        await storageService.AddProjectAsync(project, cancellationToken);
        return Ok();
    }

    [HttpGet("{projectName}/tags")]
    public async Task<ActionResult<IList<Tag>>> ListTagsInProject(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromRoute, Required, ValidName, ValidProjectName] string projectName,
        CancellationToken cancellationToken)
    {
        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        var tags = await storageService.ListTagsAsync(projectName, cancellationToken);
        return Ok(tags);
    }

    [HttpPut("{projectName}/tags")]
    public async Task<IActionResult> AddTagToProject(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromRoute, Required, ValidName, ValidProjectName] string projectName,
        [FromBody] Tag tag,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectName) || tag is null)
        {
            return BadRequest("a project name and tag (as body) are required.");
        }

        if (string.IsNullOrEmpty(tag.Name))
        {
            return BadRequest("a tag name is required.");
        }

        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        await storageService.AddTagAsync(projectName, tag, cancellationToken);
        return Ok();
    }

    [HttpGet("{projectName}/metrics")]
    public async Task<ActionResult<IList<MetricDefinition>>> GetMetricDefinitions(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromRoute, Required, ValidName, ValidProjectName] string projectName,
        CancellationToken cancellationToken)
    {
        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        var metrics = await storageService.GetMetricsAsync(projectName, cancellationToken);
        return Ok(metrics);
    }

    [HttpPut("{projectName}/metrics")]
    public async Task<IActionResult> AddMetricToProject(
        [FromServices] IStorageServiceFactory storageServiceFactory,
        [FromRoute, Required, ValidName, ValidProjectName] string projectName,
        [FromBody] IList<MetricDefinition> metrics,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(projectName) || metrics is null)
        {
            return BadRequest("a project name and metric definitions (as body) are required.");
        }

        var storageService = await storageServiceFactory.GetStorageServiceAsync(cancellationToken);
        await storageService.AddMetricsAsync(projectName, metrics, cancellationToken);
        return Ok();
    }
}
